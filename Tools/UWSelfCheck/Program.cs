using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Reads a complete Ultima Underworld 1 installation once and reports what came through.
    ///
    /// It answers two questions. For someone trying the port: does my copy of the game work
    /// with it? For the project itself: does everything still load after a change to the data
    /// layer - all nine levels, the strings, every texture, the 3D models from UW.EXE, all
    /// cutscenes and the sound. The check only reads, it changes nothing and needs no engine.
    ///
    /// Exit code 0 means everything passed, 1 means at least one check failed, 2 that the
    /// data was not found at all.
    /// </summary>
    public static partial class Program
    {
        private static int miFailures;

        public static int Main(string[] psArgs)
        {
            if (psArgs.Length < 1)
            {
                Console.WriteLine("uwselfcheck - reads a complete installation and reports what came through.");
                Console.WriteLine();
                Console.WriteLine("  uwselfcheck <path>");
                Console.WriteLine();
                Console.WriteLine("<path> is the DATA folder of an installation, or the GOG installation");
                Console.WriteLine("folder or game.gog itself.");

                return 2;
            }

            string lsDataPath = DataPath.Resolve(psArgs[0], out string lsError);

            if (lsDataPath == null)
            {
                Console.Error.WriteLine(lsError);

                return 2;
            }

            Console.WriteLine("Data folder: " + lsDataPath);
            Console.WriteLine();

            Stopwatch lOTotal = Stopwatch.StartNew();

            fCheckClickRules();
            fCheckCritterBrain();
            fCheckRuleIndex();

            DataImport lOData = fCheckLoad(lsDataPath);

            if (lOData != null)
            {
                fCheckLevels(lOData);
                fCheckStrings(lOData);
                fCheckTextures(lOData);
                fCheckModels(lsDataPath);
                fCheckCutscenes(lsDataPath);
                fCheckSound(lOData);
                fCheckPlayerStatus(lOData);
                fCheckStealth(lOData);
                fCheckEasyMovement();
                fCheckSoundInTheSave();
                fCheckHitVolume();
                fCheckPowerGem();
                fCheckImpact();
                fCheckWaterSwallowsRemains(lOData);
                fCheckScrollEdges(lOData);
                fCheckTheft(lOData);
                fCheckSightLine();
                fCheckRespawnDistance(lOData);
                fCheckMapReveal(lOData);
                fCheckMapPainter();
                fCheckNewCreature();
                fCheckSummonRoll(lOData);
                fCheckObjectDamage(lsDataPath);
                fCheckMoonstone(lOData);
                fCheckEquipmentWear(lOData);
                fCheckActiveMobileOrder(lOData);
                fCheckConversationBasePointer();
                fCheckOriginalRandom();
                fCheckBowlAndStew(lOData);
                fCheckGlowingRock(lOData);
                fCheckOneShotChainRemoval(lsDataPath);
                fCheckMapRewrite(lOData);
                fCheckPaletteLightMap(lOData);
                fCheckGrimeTint(lOData);
                fCheckTalkRefusal();
                fCheckConversationPatches(lOData);
                fCheckContainsWord();
                fCheckManualAttributes(lOData);
                fCheckExplodingBook(lOData);
                fCheckOrbManaAside();
                fCheckWeaponMusic();
                fCheckItemClassSpells(lOData);
                fCheckLocks();
                fCheckScatter();
                fCheckObjectLimit(lOData);
                fCheckSleepCreatures();
                fCheckPoisonSpell();
                fCheckDetectMonsters();
                fCheckObjectMotion(lOData);
                fCheckCreatureMotion(lOData);
                fCheckPlayerMotion(lOData);
                fCheckEasyStep(lOData);
                fCheckPointerScheme(lOData);
                fCheckCritterView();
                fCheckPreciseMotion(lOData);
            }

            lOTotal.Stop();

            Console.WriteLine();
            Console.WriteLine(miFailures == 0
                ? string.Format("Everything passed, in {0} ms.", lOTotal.ElapsedMilliseconds)
                : string.Format("{0} check(s) FAILED, in {1} ms.", miFailures, lOTotal.ElapsedMilliseconds));

            return miFailures == 0 ? 0 : 1;
        }

        // ------------------------------------------------- the checks

        /// <summary>
        /// The index of where the original is implemented (Docs/RULES-INDEX.md, built by
        /// UWRuleIndexScan, written by Tools/UWRuleIndex). Two things are asked of it here, so
        /// that neither can rot between releases:
        ///
        ///   - NO PLACE of UW.EXE may be implemented in two rule classes unless the pairing is
        ///     declared in msExpectedPairs with a reason. That is the audit against rules
        ///     written twice, which used to be a hand exercise before the release.
        ///   - THE FILE ON DISK must match the sources, so that nobody reads a stale index.
        ///     Fails with the one command that repairs it.
        ///
        /// Needs no game data, only the source tree; if that is not found - a published build
        /// without sources - the check says so and passes.
        /// </summary>
        private static void fCheckRuleIndex()
        {
            Console.WriteLine();
            Console.WriteLine("Index of the implemented places");

            string lsRoot = UWRuleIndexScan.FindProjectRoot();

            if (lsRoot == null)
            {
                fPass("rule index", "no source tree here, nothing to check");

                return;
            }

            UWRuleIndexScan.Result lOResult = UWRuleIndexScan.Scan(lsRoot);

            if (lOResult == null)
            {
                fFail("rule index", "no citations of the original found - has the comment style changed?");

                return;
            }

            fPass("rule index", lOResult.Citations + " citations of " + lOResult.Places
                + " places, " + lOResult.Shared + " of them in more than one engine-free file");

            if (lOResult.Suspects.Count == 0)
            {
                fPass("rule index: no place is implemented in two rule classes", "none");
            }
            else
            {
                foreach (string lsSuspect in lOResult.Suspects)
                    fFail("rule index: implemented in two rule classes", lsSuspect);
            }

            string lsPath = UWRuleIndexScan.GetIndexPath(lsRoot);

            if (!File.Exists(lsPath))
            {
                fFail("rule index: the file exists", "missing - run dotnet run --project Tools/UWRuleIndex");

                return;
            }

            if (File.ReadAllText(lsPath).Replace("\r\n", "\n") == lOResult.Markdown.Replace("\r\n", "\n"))
                fPass("rule index: the file matches the sources", "current");
            else
                fFail("rule index: the file matches the sources",
                    "out of date - run dotnet run --project Tools/UWRuleIndex");
        }

        /// <summary>
        /// The click rules against the user's measurements on the original (2026-09-18 and 19),
        /// one check per measurement - so a change in UWClickRules that contradicts a measurement
        /// fails here instead of in the next play. Needs no data.
        /// </summary>
        private static void fCheckClickRules()
        {
            Console.WriteLine("Click rules");

            const bool Inside = true;
            const bool Outside = false;

            // The default state: look on the press, drag of a pickable at the threshold, use on
            // the release after a drag, nothing on a plain release.
            fExpectClick("default press on a thing looks", UWClickRules.OnPress(UWCommandMode.None, UWClickRules.TargetKind.Thing, Inside),
                UWClickRules.ActionKind.Look, UWClickRules.GestureKind.Default);
            fExpectClick("default press on nothing looks (You see nothing.)", UWClickRules.OnPress(UWCommandMode.None, UWClickRules.TargetKind.Nothing, Inside),
                UWClickRules.ActionKind.Look, UWClickRules.GestureKind.Default);
            fExpectClick("press over the UI does nothing", UWClickRules.OnPress(UWCommandMode.None, UWClickRules.TargetKind.Nothing, Outside),
                UWClickRules.ActionKind.None, UWClickRules.GestureKind.Default);
            fExpectClick("default threshold on a pickable drags", UWClickRules.OnDragThreshold(UWClickRules.GestureKind.Default, UWClickRules.TargetKind.Pickable),
                UWClickRules.ActionKind.BeginDrag, UWClickRules.GestureKind.Default);
            fExpectClick("default threshold on a door waits", UWClickRules.OnDragThreshold(UWClickRules.GestureKind.Default, UWClickRules.TargetKind.Thing),
                UWClickRules.ActionKind.None, UWClickRules.GestureKind.Default);
            fExpectClick("default release after a drag uses", UWClickRules.OnRelease(UWClickRules.GestureKind.Default, true, false),
                UWClickRules.ActionKind.Use, UWClickRules.GestureKind.None);
            fExpectClick("default release after a consumed drag does nothing", UWClickRules.OnRelease(UWClickRules.GestureKind.Default, true, true),
                UWClickRules.ActionKind.None, UWClickRules.GestureKind.None);
            fExpectClick("default release as a click does nothing", UWClickRules.OnRelease(UWClickRules.GestureKind.Default, false, false),
                UWClickRules.ActionKind.None, UWClickRules.GestureKind.None);

            // Talk: the creature is remembered, the release talks; anything else is refused on
            // the press - "You cannot talk to that." for geometry, "...that!" for a thing.
            fExpectClick("talk press on a creature remembers", UWClickRules.OnPress(UWCommandMode.Talk, UWClickRules.TargetKind.Creature, Inside),
                UWClickRules.ActionKind.None, UWClickRules.GestureKind.Talk);
            fExpectClick("talk release talks", UWClickRules.OnRelease(UWClickRules.GestureKind.Talk, true, false),
                UWClickRules.ActionKind.Talk, UWClickRules.GestureKind.None);
            fExpectClick("talk press on a wall: You cannot talk to that.", UWClickRules.OnPress(UWCommandMode.Talk, UWClickRules.TargetKind.Geometry, Inside),
                UWClickRules.ActionKind.Message, UWClickRules.GestureKind.None, UWClickRules.CannotTalkToThatMessage);
            fExpectClick("talk press on a door asks the thing", UWClickRules.OnPress(UWCommandMode.Talk, UWClickRules.TargetKind.Thing, Inside),
                UWClickRules.ActionKind.TalkToThing, UWClickRules.GestureKind.None);
            fExpectClick("talk press on a pickable asks the thing", UWClickRules.OnPress(UWCommandMode.Talk, UWClickRules.TargetKind.Pickable, Inside),
                UWClickRules.ActionKind.TalkToThing, UWClickRules.GestureKind.None);

            // What the thing answers (TalkTo_ovr100_0): the shrine chants, Arial does not react,
            // any other special decoration says nothing, the rest refuses.
            fExpectBool("talk to a door: You cannot talk to that!",
                UWClickRules.TalkToThing(UWWorldCapture.FirstClosedDoorId, false) == UWClickRules.ThingTalkAnswer.CannotTalk, true);
            fExpectBool("talk to the shrine chants",
                UWClickRules.TalkToThing(UWShrineRules.ShrineObjectId, false) == UWClickRules.ThingTalkAnswer.ChantMantra, true);
            fExpectBool("talk to Arial: no reaction from the princess",
                UWClickRules.TalkToThing(UWClickRules.SpecialDecorationObjectId, true) == UWClickRules.ThingTalkAnswer.NoReactionFromPrincess, true);
            fExpectBool("talk to another special decoration says nothing",
                UWClickRules.TalkToThing(UWClickRules.SpecialDecorationObjectId, false) == UWClickRules.ThingTalkAnswer.Silent, true);
            fExpectBool("talk to the plain decoration (367) refuses",
                UWClickRules.TalkToThing(UWClickRules.SpecialDecorationObjectId + 1, true) == UWClickRules.ThingTalkAnswer.CannotTalk, true);

            // Get: the press decides at once.
            fExpectClick("get press on a pickable takes it to the pointer", UWClickRules.OnPress(UWCommandMode.Get, UWClickRules.TargetKind.Pickable, Inside),
                UWClickRules.ActionKind.TakeToPointer, UWClickRules.GestureKind.None);
            fExpectClick("get press on a wall: Nothing to get.", UWClickRules.OnPress(UWCommandMode.Get, UWClickRules.TargetKind.Geometry, Inside),
                UWClickRules.ActionKind.Message, UWClickRules.GestureKind.None, UWClickRules.NothingToGetMessage);
            fExpectClick("get press on a creature: You cannot pick that up.", UWClickRules.OnPress(UWCommandMode.Get, UWClickRules.TargetKind.Creature, Inside),
                UWClickRules.ActionKind.Message, UWClickRules.GestureKind.None, UWClickRules.CannotPickThatUpMessage);
            fExpectClick("get press on a door: You cannot pick that up.", UWClickRules.OnPress(UWCommandMode.Get, UWClickRules.TargetKind.Thing, Inside),
                UWClickRules.ActionKind.Message, UWClickRules.GestureKind.None, UWClickRules.CannotPickThatUpMessage);

            // Look: the look on the press, and that is all.
            fExpectClick("look press looks and ends the gesture", UWClickRules.OnPress(UWCommandMode.Look, UWClickRules.TargetKind.Pickable, Inside),
                UWClickRules.ActionKind.Look, UWClickRules.GestureKind.None);

            // Use: geometry is refused, a creature ignored, a thing remembered and used on the release.
            fExpectClick("use press on a wall: You cannot use that.", UWClickRules.OnPress(UWCommandMode.Use, UWClickRules.TargetKind.Geometry, Inside),
                UWClickRules.ActionKind.Message, UWClickRules.GestureKind.None, UWClickRules.CannotUseThatMessage);
            fExpectClick("use press on a creature does nothing", UWClickRules.OnPress(UWCommandMode.Use, UWClickRules.TargetKind.Creature, Inside),
                UWClickRules.ActionKind.None, UWClickRules.GestureKind.None);
            fExpectClick("use press on a door remembers", UWClickRules.OnPress(UWCommandMode.Use, UWClickRules.TargetKind.Thing, Inside),
                UWClickRules.ActionKind.None, UWClickRules.GestureKind.Use);
            fExpectClick("use threshold does not drag", UWClickRules.OnDragThreshold(UWClickRules.GestureKind.Use, UWClickRules.TargetKind.Pickable),
                UWClickRules.ActionKind.None, UWClickRules.GestureKind.Use);
            fExpectClick("use release uses, wherever the pointer is", UWClickRules.OnRelease(UWClickRules.GestureKind.Use, false, false),
                UWClickRules.ActionKind.Use, UWClickRules.GestureKind.None);
            fExpectClick("a mode changes nothing over the UI", UWClickRules.OnPress(UWCommandMode.Use, UWClickRules.TargetKind.Nothing, Outside),
                UWClickRules.ActionKind.None, UWClickRules.GestureKind.Default);

            // Which button carries the use over the UI.
            fExpectBool("left click over the UI uses", UWClickRules.IsUseClick(false, UWCommandMode.None, false), true);
            fExpectBool("right click over the UI looks", UWClickRules.IsUseClick(true, UWCommandMode.None, false), false);
            fExpectBool("right click over the UI uses in use mode", UWClickRules.IsUseClick(true, UWCommandMode.Use, false), true);
            fExpectBool("right click in a conversation looks even in use mode", UWClickRules.IsUseClick(true, UWCommandMode.Use, true), false);

            // The icons: the lit one switches off, another replaces it.
            fExpectBool("clicking the lit icon switches the mode off", UWClickRules.NextMode(UWCommandMode.Talk, UWCommandMode.Talk) == UWCommandMode.None, true);
            fExpectBool("clicking another icon replaces the mode", UWClickRules.NextMode(UWCommandMode.Talk, UWCommandMode.Look) == UWCommandMode.Look, true);
            fExpectBool("fight is not a mode here", UWClickRules.NextMode(UWCommandMode.Talk, UWCommandMode.Fight) == UWCommandMode.Talk, true);

            // The panel's areas (seg024_24DC_26F and the handlers, read 2026-09-26).
            fExpectBool("command column: the bottom 16 rows are use", UWClickRules.CommandIconAt(15) == UWCommandMode.Use, true);
            fExpectBool("command column: from 16 up fight", UWClickRules.CommandIconAt(16) == UWCommandMode.Fight, true);
            fExpectBool("command column: talk at 70 to 87", UWClickRules.CommandIconAt(70) == UWCommandMode.Talk && UWClickRules.CommandIconAt(87) == UWCommandMode.Talk, true);
            fExpectBool("command column: 88 to 105 the options", UWClickRules.CommandIconAt(105) == UWCommandMode.Options, true);
            fExpectBool("command column: above that nothing", UWClickRules.CommandIconAt(106) == UWCommandMode.None, true);
            fExpectBool("active spells: the first slot on the right", UWClickRules.ActiveSpellSlotAt(47) == 0 && UWClickRules.ActiveSpellSlotAt(32) == 0, true);
            fExpectBool("active spells: the third on the left", UWClickRules.ActiveSpellSlotAt(0) == 2, true);
            fExpectBool("active spells: the last three columns answer nothing", UWClickRules.ActiveSpellSlotAt(48) < 0, true);
            fExpectBool("flasks: vitality left, mana right", UWClickRules.FlaskAt(25, 0) == UWClickRules.FlaskPart.Vitality && UWClickRules.FlaskAt(40, 30) == UWClickRules.FlaskPart.Mana, true);
            fExpectBool("flasks: above row 30 nothing", UWClickRules.FlaskAt(10, 31) == UWClickRules.FlaskPart.None, true);
            fExpectBool("flasks: the chain above row 13 turns the panel", UWClickRules.FlaskAt(26, 14) == UWClickRules.FlaskPart.Chain && UWClickRules.FlaskAt(39, 36) == UWClickRules.FlaskPart.Chain, true);
            fExpectBool("flasks: the chain's foot does nothing", UWClickRules.FlaskAt(30, 13) == UWClickRules.FlaskPart.None, true);
            fExpectBool("poison degree: none, barely, egregiously", UWClickRules.PoisonDegree(0) == -1 && UWClickRules.PoisonDegree(3) == 0 && UWClickRules.PoisonDegree(15) == 4, true);
            fExpectBool("areas include both ends", UWClickRules.RuneShelf.Contains(176, 45) && UWClickRules.RuneShelf.Contains(222, 61) && !UWClickRules.RuneShelf.Contains(223, 61), true);
            fExpectBool("inventory: the helmet at the top middle", UWClickRules.InventorySpotAt(277, 199 - 15) == UWClickRules.InventorySpot.Helmet, true);
            fExpectBool("inventory: row 55 is the legs, not the gloves (first record wins)", UWClickRules.InventorySpotAt(275, 199 - 55) == UWClickRules.InventorySpot.Legs, true);
            fExpectBool("inventory: rows 52-53 on the left are the hand, not the ring", UWClickRules.InventorySpotAt(250, 199 - 53) == UWClickRules.InventorySpot.HandLeft, true);
            fExpectBool("inventory: backpack top left and bottom right", UWClickRules.BackpackSlotOf(UWClickRules.InventorySpotAt(240, 199 - 81)) == 0 && UWClickRules.BackpackSlotOf(UWClickRules.InventorySpotAt(314, 199 - 116)) == 7, true);
            fExpectBool("inventory: the gap between two backpack slots answers nothing", UWClickRules.InventorySpotAt(258, 199 - 90) == UWClickRules.InventorySpot.None, true);
            fExpectBool("inventory: container icon and the two arrows", UWClickRules.InventorySpotAt(248, 199 - 70) == UWClickRules.InventorySpot.ContainerIcon && UWClickRules.InventorySpotAt(300, 199 - 75) == UWClickRules.InventorySpot.ScrollLeft && UWClickRules.InventorySpotAt(310, 199 - 75) == UWClickRules.InventorySpot.ScrollRight, true);

            Console.WriteLine();
        }

        private static void fExpectClick(string psName, UWClickRules.Decision pODecision,
            UWClickRules.ActionKind peAction, UWClickRules.GestureKind peGesture, int piMessage = 0)
        {
            bool lbOk = pODecision.Action == peAction && pODecision.Gesture == peGesture
                && (piMessage == 0 || pODecision.MessageIndex == piMessage);

            if (lbOk)
                fPass(psName, pODecision.ToString());
            else
                fFail(psName, "got " + pODecision + ", expected " + peAction + (piMessage != 0 ? " " + piMessage : "") + ", gesture " + peGesture);
        }

        private static void fExpectBool(string psName, bool pbActual, bool pbExpected)
        {
            if (pbActual == pbExpected)
                fPass(psName, pbActual.ToString());
            else
                fFail(psName, "got " + pbActual + ", expected " + pbExpected);
        }

        private static void fExpectInt(string psName, int piActual, int piExpected)
        {
            if (piActual == piExpected)
                fPass(psName, piActual.ToString());
            else
                fFail(psName, "got " + piActual + ", expected " + piExpected);
        }

        // ------------------------------------------------- the creature brain

        /// <summary>
        /// A world for one creature: a flat 16 x 16 floor, a scripted RNG (a queue of values,
        /// then a default), the player as object 1 wherever the check puts him, the outcome of
        /// the last step as the check sets it, and every event the brain raises as a line.
        /// The path search steps one tile towards the destination, x first.
        /// </summary>
        private sealed class FakeCritterHost : ICritterHost
        {
            public long ClockValue;

            public readonly Queue<int> Rolls = new Queue<int>();

            /// <summary>What Random answers when the queue is empty, taken modulo n.</summary>
            public int DefaultRoll;

            public readonly UWCritterAlarm AlarmValue = new UWCritterAlarm();

            public readonly Dictionary<int, CritterTarget> Objects = new Dictionary<int, CritterTarget>();

            public StepResult Step;

            public bool LineOfSight = true;

            public bool StraightClear = true;

            public bool PathAvailable = true;

            public readonly List<string> Events = new List<string>();

            private readonly int[] miFloors = new int[16 * 16];

            public long Clock => ClockValue;

            public int Random(int piN)
            {
                int liValue = Rolls.Count > 0 ? Rolls.Dequeue() : DefaultRoll;

                return piN <= 0 ? 0 : liValue % piN;
            }

            public UWCritterAlarm Alarm => AlarmValue;

            public int FloorLevelAt(int piTileX, int piTileY)
            {
                return piTileX < 0 || piTileY < 0 || piTileX >= 16 || piTileY >= 16 ? 0 : miFloors[(piTileY * 16) + piTileX];
            }

            public bool IsMagicBlockedAt(int piTileX, int piTileY) => false;

            public bool TybalOrbStands => false;

            public int OwnObjectHeight => 16;

            public UWTilePos ViewTile { get; set; } = new UWTilePos(-100, -100);

            public CritterTarget GetObject(int piIndex)
            {
                CritterTarget lOTarget;

                return Objects.TryGetValue(piIndex, out lOTarget) ? lOTarget : default;
            }

            public bool HasLineOfSight(int piX0, int piY0, int piZ0, int piX1, int piY1, int piZ1) => LineOfSight;

            public bool IsStraightLineClear(int piToTileX, int piToTileY) => StraightClear;

            public bool TryGetNextPathTile(int piToTileX, int piToTileY, int piRangeBudget, bool pbMayOpenDoors,
                out int piNextTileX, out int piNextTileY)
            {
                piNextTileX = piToTileX;
                piNextTileY = piToTileY;

                return PathAvailable;
            }

            public StepResult RunMotion()
            {
                return Step;
            }

            public bool Teleport(int piTileX, int piTileY)
            {
                Events.Add("teleport " + piTileX + "/" + piTileY);

                return true;
            }

            public bool UseDoor(int piDoorIndex)
            {
                Events.Add("use door " + piDoorIndex);

                return false;
            }

            public void PickDoor(int piDoorIndex, int piSkill) => Events.Add("pick door " + piDoorIndex);

            public void BashDoor(int piDoorIndex, int piDamage) => Events.Add("bash door " + piDoorIndex + " " + piDamage);

            public int MissileVelocity(int piAmmoIndex) => 20;

            public void OnSwingStarted(int piAttack) => Events.Add("swing " + piAttack);

            public void OnBlow(int piTargetIndex, int piAttack, int piCharge, int piSwingType, int piFlank)
                => Events.Add("blow " + piTargetIndex + " " + piAttack + " charge " + piCharge + " flank " + piFlank);

            public void OnMissileLaunched(int piAmmoIndex, int piPitch) => Events.Add("missile " + piAmmoIndex);

            public void OnSpellCast(int piSpellIndex, int piPitch) => Events.Add("spell " + piSpellIndex);

            public void OnTalk() => Events.Add("talk");

            public void PlaySound(int piSound) => Events.Add("sound " + piSound);

            public void PlayMusic(int piTheme) => Events.Add("music " + piTheme);

            public void OnDrowned() => Events.Add("drowned");

            public void OnDeathStarted(bool pbByPlayer) => Events.Add("death");

            public bool VetoDeath() => false;

            public void OnDeathFinished() => Events.Add("removed");

            public void OnGoalChanged(int piOldGoal, int piNewGoal, int piGTarg) => Events.Add("goal " + piOldGoal + ">" + piNewGoal);

            /// <summary>The player at a position in eighths, on the floor, looking this way.</summary>
            public void PlacePlayer(int piX, int piY, int piFacing = 0, int piLoudness = 8, int piVisibility = 8, bool pbWeaponDrawn = false)
            {
                Objects[1] = new CritterTarget
                {
                    Exists = true,
                    Index = 1,
                    IsPlayer = true,
                    IsCreature = true,
                    ItemId = 0x7F,
                    X = piX,
                    Y = piY,
                    ZPos = 0,
                    TileX = piX >> 3,
                    TileY = piY >> 3,
                    ObjectHeight = 16,
                    FacingEighth = piFacing,
                    Loudness = piLoudness,
                    Visibility = piVisibility,
                    HitPoints = 30,
                    Goal = 0,
                    Kind = 0,
                    WeaponDrawn = pbWeaponDrawn
                };
            }

            public bool HasEvent(string psPrefix)
            {
                foreach (string lsEvent in Events)
                {
                    if (lsEvent.StartsWith(psPrefix, StringComparison.Ordinal))
                        return true;
                }

                return false;
            }

            public int CountEvents(string psPrefix)
            {
                int liCount = 0;

                foreach (string lsEvent in Events)
                {
                    if (lsEvent.StartsWith(psPrefix, StringComparison.Ordinal))
                        liCount++;
                }

                return liCount;
            }
        }

        /// <summary>A creature of a made-up kind with the table values the checks need.</summary>
        private static UWObjectClassProperties.Critter fMakeRow(int piKind = 3, int piVitality = 20, int piMorale = 5,
            int piNoise = 8, int piSight = 8, int piInitiative = 15, int piRestlessness = 5, int piTravelRange = 3)
        {
            return new UWObjectClassProperties.Critter
            {
                Row = new byte[48],
                Vitality = (byte)piVitality,
                Morale = (byte)piMorale,
                NoiseRange = (byte)piNoise,
                SightRange = (byte)piSight,
                Initiative = (byte)piInitiative,
                Restlessness = (byte)piRestlessness,
                TravelRange = (byte)piTravelRange,
                WanderSpeed = 6,
                MovementSpeed = 8,
                Dexterity = 0,
                GeneralType = (byte)piKind,
                Attacks = new[]
                {
                    new UWObjectClassProperties.CritterAttack { ToHit = 10, Damage = 4, Probability = 100 },
                    new UWObjectClassProperties.CritterAttack(),
                    new UWObjectClassProperties.CritterAttack()
                },
                Spell3 = -1
            };
        }

        /// <summary>A creature record at a tile (fine position 4/4, floor 0), with a home post.</summary>
        private static UWCritterRecord fMakeRecord(int piTileX, int piTileY, int piGoal, int piAttitude,
            int piHomeX = -1, int piHomeY = -1, int piHitPoints = 20)
        {
            UWNpc lONpc = new UWNpc(0x43)
            {
                RawNpcBytes = new byte[19],
                XPos = 4,
                YPos = 4,
                ZPos = 0,
                Heading = 0,
                Quality = (ushort)(piHomeX < 0 ? piTileX : piHomeX),
                Owner = (ushort)(piHomeY < 0 ? piTileY : piHomeY)
            };

            UWCritterRecord lORecord = new UWCritterRecord(lONpc);

            lORecord.TileX = piTileX;
            lORecord.TileY = piTileY;
            lORecord.Goal = piGoal;
            lORecord.GTarg = 1;
            lORecord.Attitude = piAttitude;
            lORecord.HitPoints = piHitPoints;
            lORecord.Interval = 4;

            return lORecord;
        }

        /// <summary>
        /// The creature brain against the specification (Docs/AI/creature-ai.md): each
        /// check is one sentence of the spec run on the fake host. Needs no data.
        /// </summary>
        private static void fCheckCritterBrain()
        {
            Console.WriteLine("Creature brain");

            fCheckSlotClock();
            fCheckDistanceCull();
            fCheckMeleeRange();
            fCheckMeleeCadence();
            fCheckDamageReaction();
            fCheckMoraleFormula();
            fCheckBlockedStep();
            fCheckTalkGoal();
            fCheckUnknownGoals();
            fCheckMantras();
            fCheckPerceptionThresholds();
            fCheckLeash();
            fCheckTurnLimiter();
            fCheckKinAlarm();
            fCheckCombatExecution();
            fCheckPlayerAsDefender();
            fCheckDrowning();
            fCheckPathWindow();

            Console.WriteLine();
        }

        /// <summary>A path walker that only refuses water - the water half of
        /// UWCritter.CanEnterTile, so the path search can be pinned without a level.</summary>
        private sealed class FakeWaterWalker : UWTilePath.IWalker
        {
            public readonly bool[,] Water = new bool[64, 64];

            public bool Swimmer;

            public bool Flier = false;

            public bool CanEnterTile(int piFromX, int piFromY, int piToX, int piToY)
            {
                if (Flier)
                    return true;

                return Swimmer == Water[piToX, piToY];
            }
        }

        /// <summary>
        /// Section 7, deviation 43, built 2026-09-20: a land creature that ends its step in
        /// deep water drowns. The original refuses water only in the TILE tests; the step
        /// itself walks in (seg006_1477_476, asm lines 41417-41460), which is why a creature
        /// can only get there while it closes in on the player in melee - the user watched
        /// exactly that in the original on 2026-09-20. The death removal afterwards is the
        /// ordinary one; what it puts down in deep water is destroyed there, which is why
        /// nothing was left (see fCheckWaterSwallowsRemains).
        /// </summary>
        private static void fCheckDrowning()
        {
            // The rule itself (41418-41421 "and ax, 0F8h; cmp ax, 10h").
            fExpectBool("drowning: water under the feet drowns a land creature",
                UWCritterRules.Drowns(0x10, false, false), true);
            fExpectBool("drowning: plain floor does not",
                UWCritterRules.Drowns(0x08, false, false), false);
            fExpectBool("drowning: lava does not - it burns instead",
                UWCritterRules.Drowns(0x20, false, false), false);
            fExpectBool("drowning: standing on an object in the water does not (bit 0x80)",
                UWCritterRules.Drowns(0x10 | 0x80, false, false), false);
            fExpectBool("drowning: the bits outside the 0xF8 mask are ignored",
                UWCritterRules.Drowns(0x10 | 0x1000 | 0x07, false, false), true);
            fExpectBool("drowning: a swimmer never drowns",
                UWCritterRules.Drowns(0x10, true, false), false);
            fExpectBool("drowning: a flier never drowns",
                UWCritterRules.Drowns(0x10, false, true), false);

            // The chain in the brain: the step's verdict and the removal in the SAME update.
            // In the original the drowning is written during the physics, which is step 6, and
            // the animation machine of the same update (step 9) finds animation 0x0C at frame 3
            // and removes the creature. Nothing of the death picture is ever drawn - the user
            // watched for it in the original and saw only the splash (2026-09-20).
            FakeCritterHost lOHost = new FakeCritterHost();
            UWCritterRecord lORecord = fMakeRecord(5, 5, 5, 0);
            UWCritterBrain lOBrain = new UWCritterBrain(lORecord, fMakeRow(), 5);

            lORecord.Animation = UWCritterBrain.AnimWalking;
            lORecord.Speed = 8;
            lOHost.Step = new StepResult { Drowned = true };

            UWCritterBrain.Motion lOMotion = lOBrain.Update(lOHost);

            fExpectInt("drowning: the death animation 0x0C is set", lORecord.Animation, UWCritterBrain.AnimDying);
            fExpectInt("drowning: at frame 3", lORecord.Frame, UWCritterRules.DeathRemovalFrame);
            fExpectInt("drowning: with interval 1", lORecord.Interval, UWCritterRules.IntervalStuck);
            fExpectInt("drowning: the hit points are gone", lORecord.HitPoints, 0);
            fExpectInt("drowning: exactly one splash", lOHost.CountEvents("drowned"), 1);
            fExpectBool("drowning: the body goes in the same update", lOMotion.Removed, true);
            fExpectInt("drowning: the death removal ran once", lOHost.CountEvents("removed"), 1);

            // A further update on the same record must not drown it a second time.
            lOMotion = lOBrain.Update(lOHost);

            fExpectInt("drowning: no second splash", lOHost.CountEvents("drowned"), 1);

            // A swimmer and a flier: the host never reports the water, so nothing happens.
            foreach (bool lbFlier in new[] { false, true })
            {
                FakeCritterHost lOWaterHost = new FakeCritterHost();
                UWCritterRecord lOWaterRecord = fMakeRecord(5, 5, 5, 0);
                UWObjectClassProperties.Critter lORow = fMakeRow();

                lORow.Row[0x0A] = (byte)(lbFlier ? 0x80 : 0x40);
                lOWaterHost.PlacePlayer((5 << 3) + 4, (5 << 3) + 4);

                UWCritterBrain lOWaterBrain = new UWCritterBrain(lOWaterRecord, lORow, 5);

                lOWaterHost.Step = new StepResult
                {
                    Drowned = UWCritterRules.Drowns(0x10, !lbFlier, lbFlier)
                };
                lOWaterBrain.Update(lOWaterHost);

                fExpectBool((lbFlier ? "drowning: a flier" : "drowning: a swimmer") + " crosses the water alive",
                    lOWaterRecord.Animation == UWCritterBrain.AnimDying, false);
            }

            // The TILE tests still keep water out, so no creature ever plans a step into it
            // (handler word 2 = 0x1010 for land, 04-movement.md section 5).
            FakeWaterWalker lOWalker = new FakeWaterWalker();

            for (int liY = 1; liY <= 3; liY++)
                lOWalker.Water[4, liY] = true;

            UWTilePos lONext;

            fExpectBool("drowning: the path search finds a way around the water",
                UWTilePath.TryGetNextStep(lOWalker, new UWTilePos(2, 2), new UWTilePos(6, 2), out lONext), true);
            fExpectBool("drowning: and its next tile is dry", lOWalker.Water[lONext.X, lONext.Y], false);

            for (int liY = 0; liY < 64; liY++)
                lOWalker.Water[4, liY] = true;

            fExpectBool("drowning: a wall of water leaves a land creature no path",
                UWTilePath.TryGetNextStep(lOWalker, new UWTilePos(2, 2), new UWTilePos(6, 2), out lONext), false);

            lOWalker.Swimmer = true;

            fExpectBool("drowning: a swimmer's path needs the water instead",
                UWTilePath.TryGetNextStep(lOWalker, new UWTilePos(4, 2), new UWTilePos(4, 8), out lONext), true);
        }

        /// <summary>
        /// The search window, built 2026-09-21 in place of our ceiling of 400 examined tiles:
        /// PathFindBetweenTiles_seg006_1477_12BB works only inside the box around start and
        /// target, five tiles wider on every side (labels 12F9-13C8), and skips every
        /// neighbour outside it (labels 15DE-15F8). The lower edge clamps at tile 1, so the
        /// solid border row and column 0 are out.
        ///
        /// The original has no memory of dead ends and needs none: this is a breadth-first
        /// search, which does not walk into one.
        /// </summary>
        private static void fCheckPathWindow()
        {
            UWTilePos lONext;

            // A wall from y 6 to 14 across the way from 10/10 to 20/10: the way round at y 5
            // is just inside the window, which reaches from 5 to 15.
            FakeWaterWalker lONear = new FakeWaterWalker();

            for (int liY = 6; liY <= 14; liY++)
                lONear.Water[15, liY] = true;

            fExpectBool("path window: a way round inside the window is found",
                UWTilePath.TryGetNextStep(lONear, new UWTilePos(10, 10), new UWTilePos(20, 10), out lONext), true);

            // The same wall two tiles longer at each end: now the way round would have to
            // leave the window, and the original does not look there.
            FakeWaterWalker lOFar = new FakeWaterWalker();

            for (int liY = 4; liY <= 16; liY++)
                lOFar.Water[15, liY] = true;

            fExpectBool("path window: a way round outside it is not found",
                UWTilePath.TryGetNextStep(lOFar, new UWTilePos(10, 10), new UWTilePos(20, 10), out lONext), false);

            fExpectInt("path window: five tiles beyond each end", UWTilePath.SearchMargin, 5);
            fExpectInt("path window: and never below tile 1", UWTilePath.FirstSearchTile, 1);

            // Row 0 is the map's solid border: a way that only exists there is none.
            FakeWaterWalker lOBorder = new FakeWaterWalker();

            for (int liY = 1; liY < 64; liY++)
                lOBorder.Water[4, liY] = true;

            fExpectBool("path window: the border row 0 is no way round",
                UWTilePath.TryGetNextStep(lOBorder, new UWTilePos(2, 2), new UWTilePos(6, 2), out lONext), false);

            // The waves stop at 0x20 (PathFindBetweenTiles, 2026-09-29): 32 steps are found,
            // 33 are not - Biden's way home of 42 never was.
            GridWalker lOOpen = new GridWalker();

            fExpectInt("path length: at most 32 steps", UWTilePath.MaxPathLength, 32);
            fExpectInt("path width: at most 64 tiles a wave", UWTilePath.MaxWaveWidth, 64);
            fExpectBool("path length: 32 steps are found",
                UWTilePath.TryGetNextStep(lOOpen, new UWTilePos(10, 10), new UWTilePos(42, 10), out lONext), true);
            fExpectBool("path length: 33 steps are not",
                UWTilePath.TryGetNextStep(lOOpen, new UWTilePos(10, 10), new UWTilePos(43, 10), out lONext), false);
        }

        /// <summary>
        /// Spec 8.8: the player is row 63 of the creature table, and
        /// PlayerStatusUpdate_ovr133_784 (382211) fills it. Pinned here without data: the
        /// defence value of row byte 0x12 and the quality scaling of the armour bytes. The
        /// whole pass over a worn set is in fCheckPlayerStatus, which needs OBJECTS.DAT.
        /// </summary>
        private static void fCheckPlayerAsDefender()
        {
            // Row byte 0x12 = PLAYER.DAT[0x22] + PLAYER.DAT[0x21 + var_6] / 2 (labels 86A-938).
            fExpectInt("defence: an empty hand is the Defence skill alone", UWPlayerCritterRow.GetDefence(30, 0), 30);
            fExpectInt("defence: skill 30 with a weapon skill of 25 is 42", UWPlayerCritterRow.GetDefence(30, 25), 42);
            fExpectInt("defence: the weapon skill is halved downwards", UWPlayerCritterRow.GetDefence(0, 7), 3);
            fExpectInt("defence: a weapon skill of 1 adds nothing", UWPlayerCritterRow.GetDefence(10, 1), 10);

            // THE USER MEASURED THIS IN THE ORIGINAL ON 2026-09-20: a reaper (item 119, attack 0
            // to-hit 18 and row byte 0x11 = 25, so an attack score of 18 + 12 = 30) NEVER
            // flashed the critical colour 0xB8 at a character with Defence skill 30 - while our
            // port did. A critical needs score - check + RNG(31) > 28, and the roll reaches 30,
            // so it is impossible from check >= score + 2 on. The skill alone leaves 2 of 31
            // rolls; half a weapon skill of 4 already closes it.
            const int ReaperAttackScore = 30;

            fExpectInt("reaper: the bare Defence skill leaves 2 critical rolls of 31",
                fCountCriticalRolls(ReaperAttackScore, UWPlayerCritterRow.GetDefence(30, 0)), 2);
            fExpectInt("reaper: skill 30 plus unarmed 3 leaves 1 of 31",
                fCountCriticalRolls(ReaperAttackScore, UWPlayerCritterRow.GetDefence(30, 3)), 1);
            fExpectInt("reaper: skill 30 plus unarmed 4 makes a critical impossible",
                fCountCriticalRolls(ReaperAttackScore, UWPlayerCritterRow.GetDefence(30, 4)), 0);
            fExpectInt("reaper: skill 30 plus sword 25 makes a critical impossible",
                fCountCriticalRolls(ReaperAttackScore, UWPlayerCritterRow.GetDefence(30, 25)), 0);
            // It still lands blows: with the check at 42 the roll has to reach 28 of 30, so
            // roughly one swing in ten gets through - and none of them as a critical.
            fExpectInt("reaper: skill 30 plus sword 25 still lets 3 of 31 rolls hit",
                fCountHitRolls(ReaperAttackScore, UWPlayerCritterRow.GetDefence(30, 25)), 3);
            fExpectInt("reaper: against the bare skill 15 of 31 rolls hit",
                fCountHitRolls(ReaperAttackScore, UWPlayerCritterRow.GetDefence(30, 0)), 15);

            // The armour bytes: ((table protection * quality) >> 6) + 1 per piece (ovr133_72D).
            // A full plate mail is 6 of 6, a worn one less, a ruined one still 1.
            fExpectInt("armour: plate mail (6) at full quality gives 6", UWPlayerCritterRow.ScaleArmourValue(6, 63), 6);
            fExpectInt("armour: the same at quality 40 gives 4", UWPlayerCritterRow.ScaleArmourValue(6, 40), 4);
            fExpectInt("armour: the same at quality 10 gives 1", UWPlayerCritterRow.ScaleArmourValue(6, 10), 1);
            fExpectInt("armour: a ruined piece still gives 1", UWPlayerCritterRow.ScaleArmourValue(6, 0), 1);
            fExpectInt("armour: a tower shield (3) at full quality gives 3", UWPlayerCritterRow.ScaleArmourValue(3, 63), 3);

            // Only 0x3B to 0x3F count as a shield in the off hand (labels 80E-840).
            fExpectBool("shield: 0x3B counts", UWPlayerCritterRow.IsShield(0x3B), true);
            fExpectBool("shield: 0x3F counts", UWPlayerCritterRow.IsShield(0x3F), true);
            fExpectBool("shield: 0x3A does not", UWPlayerCritterRow.IsShield(0x3A), false);
            fExpectBool("shield: a sword does not", UWPlayerCritterRow.IsShield(0x03), false);
        }

        /// <summary>How many of the 31 rolls of SkillCheck_seg037_32E6_C end as a critical hit -
        /// the red flash at the player.</summary>
        private static int fCountCriticalRolls(int piAttackScore, int piDefence)
        {
            int liCriticals = 0;

            for (int liRoll = 0; liRoll < UWCritterRules.SkillCheckRoll; liRoll++)
            {
                if (UWSkillCheck.GetResult(UWCritterCombat.GetHitScore(piAttackScore, 0, 0, piDefence, liRoll))
                    == UWSkillCheck.ResultEnum.CriticalSuccess)
                    liCriticals++;
            }

            return liCriticals;
        }

        /// <summary>How many of the 31 rolls land as a hit or a critical hit.</summary>
        private static int fCountHitRolls(int piAttackScore, int piDefence)
        {
            int liHits = 0;

            for (int liRoll = 0; liRoll < UWCritterRules.SkillCheckRoll; liRoll++)
            {
                if (UWSkillCheck.IsSuccess(UWSkillCheck.GetResult(
                    UWCritterCombat.GetHitScore(piAttackScore, 0, 0, piDefence, liRoll))))
                    liHits++;
            }

            return liHits;
        }

        /// <summary>Spec 1.1-1.2: interval 4 is due every fourth slot at one slot per frame, the
        /// wrap of the 16-slot clock included; a slow frame is clamped to 4 units; Speed halves.</summary>
        private static void fCheckSlotClock()
        {
            UWCritterClock lOClock = new UWCritterClock();
            int liSlot = 0;
            int liDue = 0;
            // Slot 0 fires when the phase has moved past it, at frame 1; then every fourth.
            int liLastDueFrame = -3;
            bool lbGapsOfFour = true;

            for (int liFrame = 1; liFrame <= 64; liFrame++)
            {
                lOClock.Advance(16, false);

                while (lOClock.IsDue(liSlot))
                {
                    liDue++;

                    if (liFrame - liLastDueFrame != 4)
                        lbGapsOfFour = false;

                    liLastDueFrame = liFrame;
                    liSlot = UWCritterClock.NextSlot(liSlot, 4);
                }
            }

            fExpectInt("clock: interval 4 fires 16 times in 64 slots", liDue, 16);
            fExpectBool("clock: every fourth slot, across the wrap", lbGapsOfFour, true);

            lOClock = new UWCritterClock();
            fExpectInt("clock: a slow frame of 200 ticks hands out 4 units", lOClock.Advance(200, false), 4);
            fExpectBool("clock: slot 0 is due after 4 units", lOClock.IsDue(0), true);
            fExpectBool("clock: slot 4 is not due after 4 units", lOClock.IsDue(4), false);

            lOClock = new UWCritterClock();
            int liFirst = lOClock.Advance(16, true);
            int liSecond = lOClock.Advance(16, true);

            fExpectInt("clock: Speed halves the units, odd one kept", liFirst * 10 + liSecond, 1);
        }

        /// <summary>Spec 1.3: beyond 10 tiles the update returns with the slot advanced by 8;
        /// at exactly 10 tiles it runs.</summary>
        private static void fCheckDistanceCull()
        {
            FakeCritterHost lOHost = new FakeCritterHost();
            UWCritterRecord lORecord = fMakeRecord(2, 2, 0, 2);
            UWCritterBrain lOBrain = new UWCritterBrain(lORecord, fMakeRow(), 5);

            lOHost.PlacePlayer((13 << 3) + 4, (2 << 3) + 4);

            UWCritterBrain.Motion lOMotion = lOBrain.Update(lOHost);

            fExpectBool("cull: 11 tiles away is culled", lOMotion.Culled, true);
            fExpectInt("cull: the slot advances by 8", lORecord.DueSlot, 8);

            lOHost.PlacePlayer((12 << 3) + 4, (2 << 3) + 4);
            lOMotion = lOBrain.Update(lOHost);
            fExpectBool("cull: 10 tiles away runs", lOMotion.Culled, false);
        }

        /// <summary>Spec 6.5 and 8.2: at 9 eighths goal 5 walks in and never strikes, because
        /// NPC_Goto overwrites the rolled swing; at 8 eighths it stands and the swing survives.</summary>
        private static void fCheckMeleeRange()
        {
            FakeCritterHost lOHost = new FakeCritterHost();
            UWCritterRecord lORecord = fMakeRecord(5, 5, 5, 0);
            UWCritterBrain lOBrain = new UWCritterBrain(lORecord, fMakeRow(), 5);

            lOHost.PlacePlayer(44 + 9, 44);

            bool lbAlwaysWalking = true;

            for (int liAt = 0; liAt < 8; liAt++)
            {
                lOBrain.Update(lOHost);

                if (lORecord.Animation != UWCritterBrain.AnimWalking)
                    lbAlwaysWalking = false;
            }

            fExpectBool("melee: at 9 eighths the creature walks in every update", lbAlwaysWalking, true);
            fExpectBool("melee: at 9 eighths no swing survives, no blow", lOHost.HasEvent("swing") || lOHost.HasEvent("blow"), false);
            fExpectInt("melee: it walks at the pursuit speed", lORecord.Speed, 8);

            lOHost = new FakeCritterHost();
            lORecord = fMakeRecord(5, 5, 5, 0);
            lOBrain = new UWCritterBrain(lORecord, fMakeRow(), 5);
            lOHost.PlacePlayer(44 + 8, 44);
            lOBrain.Update(lOHost);

            fExpectInt("melee: at 8 eighths the 1-in-4 roll starts the swing (animation 1)", lORecord.Animation, 1);
            fExpectInt("melee: the swing starts at frame 0", lORecord.Frame, 0);
            fExpectInt("melee: standing, speed 0", lORecord.Speed, 0);
        }

        /// <summary>Spec 8.2: failed rolls raise the charge index, the swing lands on the fifth
        /// update after the decision with the charge table value, and the blow clears it.</summary>
        private static void fCheckMeleeCadence()
        {
            FakeCritterHost lOHost = new FakeCritterHost();
            UWCritterRecord lORecord = fMakeRecord(5, 5, 5, 0);
            UWCritterBrain lOBrain = new UWCritterBrain(lORecord, fMakeRow(), 5);

            // The player looks east, away from the creature east of him: a blow from behind.
            lOHost.PlacePlayer(44 + 8, 44, 2);
            lORecord.SetDestination(6, 5, 0);
            lORecord.NewDestination = false;
            lOHost.DefaultRoll = 1;

            for (int liAt = 0; liAt < 3; liAt++)
                lOBrain.Update(lOHost);

            fExpectInt("cadence: three failed rolls raise the charge index to 3", lORecord.ChargeIndex, 3);
            fExpectInt("cadence: the combat idle stays animation 0", lORecord.Animation, 0);

            // Dexterity roll, the start roll, the attack pick.
            lOHost.Rolls.Enqueue(1);
            lOHost.Rolls.Enqueue(0);
            lOHost.Rolls.Enqueue(0);
            lOBrain.Update(lOHost);

            fExpectInt("cadence: the swing starts", lORecord.Animation, 1);

            int liBlowUpdate = 0;

            for (int liAt = 1; liAt <= 6 && liBlowUpdate == 0; liAt++)
            {
                lOBrain.Update(lOHost);

                if (lOHost.HasEvent("blow"))
                    liBlowUpdate = liAt;
            }

            fExpectInt("cadence: the blow lands on the fifth update after the decision", liBlowUpdate, 5);
            fExpectBool("cadence: the blow carries charge 80 (index 3) and flank 4 from behind",
                lOHost.HasEvent("blow 1 0 charge 80 flank 4"), true);
            fExpectInt("cadence: the blow clears the charge index", lORecord.ChargeIndex, 0);
            fExpectBool("cadence: the swing started the combat music", lOHost.HasEvent("music 6"), true);
            fExpectInt("cadence: after the blow the combat idle resumes", lORecord.Animation, 0);
        }

        /// <summary>Spec 4.3: a far hit sets bit 5 and goal 5 without a roll; a near hit rolls
        /// the morale check; bit 4 gives goal 9; bit 5 gives goal 5 at any distance.</summary>
        private static void fCheckDamageReaction()
        {
            FakeCritterHost lOHost = new FakeCritterHost();
            UWCritterRecord lORecord = fMakeRecord(5, 5, 0, 2, -1, -1, 10);
            UWCritterBrain lOBrain = new UWCritterBrain(lORecord, fMakeRow(3, 20, 0), 5);

            lOHost.PlacePlayer((8 << 3) + 4, 44);
            lOBrain.OnDamaged(lOHost, 1, 3);

            fExpectInt("reaction: DamageNPC stores the attacker", lORecord.Attacker, 1);
            fExpectInt("reaction: DamageNPC adds the damage", lORecord.DamageTaken, 3);
            fExpectInt("reaction: DamageNPC subtracts the hit points", lORecord.HitPoints, 7);

            lOHost.Rolls.Enqueue(9);
            lOBrain.Update(lOHost);

            fExpectBool("reaction: a far hit sets the relentless bit", lORecord.Relentless, true);
            fExpectInt("reaction: a far hit gives goal 5", lORecord.Goal, 5);
            fExpectInt("reaction: the attacker becomes the target", lORecord.GTarg, 1);
            fExpectInt("reaction: the attacker byte is cleared", lORecord.Attacker, 0);
            fExpectInt("reaction: the damage byte is cleared", lORecord.DamageTaken, 0);
            fExpectInt("reaction: a far hit makes the creature hostile", lORecord.Attitude, 0);

            // Near: hp 10 of 20, morale 0, damage 3 -> 15 >= 8 + roll, flee on any roll.
            lOHost = new FakeCritterHost();
            lORecord = fMakeRecord(5, 5, 0, 2, -1, -1, 13);
            lOBrain = new UWCritterBrain(lORecord, fMakeRow(3, 20, 0), 5);
            lOHost.PlacePlayer((6 << 3) + 4, 44);
            lOBrain.OnDamaged(lOHost, 1, 3);
            lOHost.Rolls.Enqueue(2);
            lOBrain.Update(lOHost);

            fExpectInt("reaction: a near hit rolls the morale check and flees (goal 6)", lORecord.Goal, 6);
            fExpectBool("reaction: the roll was consumed", lOHost.Rolls.Count == 0, true);
            fExpectBool("reaction: a near hit leaves bit 5 clear", lORecord.Relentless, false);

            lOHost = new FakeCritterHost();
            lORecord = fMakeRecord(5, 5, 0, 2, -1, -1, 13);
            lOBrain = new UWCritterBrain(lORecord, fMakeRow(3, 20, 0), 5);
            lOHost.PlacePlayer((6 << 3) + 4, 44);
            lORecord.MadeStand = true;
            lOBrain.OnDamaged(lOHost, 1, 3);
            lOBrain.Update(lOHost);

            fExpectInt("reaction: bit 4 gives goal 9 without a roll", lORecord.Goal, 9);

            lOHost = new FakeCritterHost();
            lORecord = fMakeRecord(5, 5, 0, 2, -1, -1, 13);
            lOBrain = new UWCritterBrain(lORecord, fMakeRow(3, 20, 0), 5);
            lOHost.PlacePlayer((6 << 3) + 4, 44);
            lORecord.MadeStand = true;
            lORecord.Relentless = true;
            lOBrain.OnDamaged(lOHost, 1, 3);
            lOBrain.Update(lOHost);

            fExpectInt("reaction: bit 5 gives goal 5 before bit 4", lORecord.Goal, 5);

            // THE WHOLE SEQUENCE ON ONE CREATURE, which is what the counter-test in the game
            // asks (2026-09-21): first a hit from afar, then one in melee that would certainly
            // make it flee on its own - it must not. Same numbers as the near case above, where
            // the same creature does flee.
            lOHost = new FakeCritterHost();
            lORecord = fMakeRecord(5, 5, 0, 2, -1, -1, 13);
            lOBrain = new UWCritterBrain(lORecord, fMakeRow(3, 20, 0), 5);

            lOHost.PlacePlayer((8 << 3) + 4, 44);
            lOBrain.OnDamaged(lOHost, 1, 3);
            lOBrain.Update(lOHost);

            fExpectBool("flee block: the far hit sets the bit", lORecord.Relentless, true);

            // And now from one tile away, with hit points that would flee on any roll.
            lOHost.PlacePlayer((6 << 3) + 4, 44);
            lORecord.HitPoints = 10;
            lOBrain.OnDamaged(lOHost, 1, 3);
            lOBrain.Update(lOHost);

            fExpectInt("flee block: the melee hit afterwards pursues instead of fleeing", lORecord.Goal, 5);
            fExpectBool("flee block: no morale roll was needed", lOHost.Rolls.Count == 0, true);
            fExpectBool("flee block: the bit stays", lORecord.Relentless, true);

            // Another creature's blow does not provoke a bystander.
            lOHost = new FakeCritterHost();
            lORecord = fMakeRecord(5, 5, 0, 2, -1, -1, 13);
            lOBrain = new UWCritterBrain(lORecord, fMakeRow(3, 20, 0), 5);
            lOHost.PlacePlayer((6 << 3) + 4, 44);
            lOHost.Objects[7] = new CritterTarget { Exists = true, Index = 7, IsCreature = true, ItemId = 0x45, X = 52, Y = 44, TileX = 6, TileY = 5, HitPoints = 9 };
            lOBrain.OnDamaged(lOHost, 7, 3);
            lOBrain.Update(lOHost);

            fExpectInt("reaction: a hit by a creature that is no ally is ignored", lORecord.Goal, 0);
        }

        /// <summary>Spec 4.4: the four boundaries of the morale check.</summary>
        private static void fCheckMoraleFormula()
        {
            fExpectBool("morale: hp above three quarters never withdraws", UWCritterRules.MoraleCheck(16, 13, 0, 0, 0), false);
            fExpectBool("morale: hp at three quarters goes on to the roll", UWCritterRules.MoraleCheck(16, 12, 0, 0, 0), true);
            fExpectBool("morale: hp below an eighth fights to the death", UWCritterRules.MoraleCheck(16, 1, 0, 9, 0), false);
            fExpectBool("morale: hp at an eighth goes on", UWCritterRules.MoraleCheck(16, 2, 0, 9, 0), true);
            fExpectBool("morale: damage above half the vitality withdraws", UWCritterRules.MoraleCheck(16, 8, 15, 9, 3), true);
            fExpectBool("morale: damage at half goes on to the roll", UWCritterRules.MoraleCheck(16, 8, 15, 8, 3), false);
            fExpectBool("morale: 15 - morale >= hp * 16 / vitality + roll, at equality", UWCritterRules.MoraleCheck(16, 8, 7, 0, 0), true);
            fExpectBool("morale: one above and it stays", UWCritterRules.MoraleCheck(16, 8, 7, 0, 1), false);
            fExpectBool("morale: vitality 0 never withdraws", UWCritterRules.MoraleCheck(0, 0, 0, 0, 0), false);
        }

        /// <summary>
        /// Spec 8.3-8.7, deviation rows 45-51: the execution arithmetic of
        /// UWCritterCombat - the skill check bands at the four thresholds, the flank bonus at
        /// four facing differences, the strong bonuses at their ends, the dice bounds for a
        /// base value, the charge table, the missile charge, the poison gate, the armour byte
        /// fallback, the experience with and without the strong factor, the corpse roll.
        /// </summary>
        private static void fCheckCombatExecution()
        {
            // To-hit: score - protection + flank - defence + roll, banded by SkillCheck_seg037_32E6_C.
            fExpectInt("to-hit: 2 is a critical miss", (int)UWSkillCheck.GetResult(UWCritterCombat.GetHitScore(10, 0, 0, 8, 0)), (int)UWSkillCheck.ResultEnum.CriticalFailure);
            fExpectInt("to-hit: 3 is a miss", (int)UWSkillCheck.GetResult(UWCritterCombat.GetHitScore(10, 0, 0, 8, 1)), (int)UWSkillCheck.ResultEnum.Failure);
            fExpectInt("to-hit: 15 is a miss", (int)UWSkillCheck.GetResult(UWCritterCombat.GetHitScore(10, 0, 0, 8, 13)), (int)UWSkillCheck.ResultEnum.Failure);
            fExpectInt("to-hit: 16 is a hit", (int)UWSkillCheck.GetResult(UWCritterCombat.GetHitScore(10, 0, 0, 8, 14)), (int)UWSkillCheck.ResultEnum.Success);
            fExpectInt("to-hit: 28 is a hit", (int)UWSkillCheck.GetResult(UWCritterCombat.GetHitScore(10, 0, 0, 8, 26)), (int)UWSkillCheck.ResultEnum.Success);
            fExpectInt("to-hit: 29 is a critical hit", (int)UWSkillCheck.GetResult(UWCritterCombat.GetHitScore(10, 0, 0, 8, 27)), (int)UWSkillCheck.ResultEnum.CriticalSuccess);
            fExpectInt("to-hit: flank adds, protection subtracts", UWCritterCombat.GetHitScore(10, 4, 3, 8, 0), 3);
            fExpectInt("to-hit: the roll is taken modulo 31", UWCritterCombat.GetHitScore(0, 0, 0, 0, 31), 0);

            // CalcFlankingBonus_seg022_230E_D48: d = (defender + 12 - attacker) & 7, d or 8 - d.
            fExpectInt("flank: from the front is 0", UWCritterRules.GetFlankingBonus(0, 4), 0);
            fExpectInt("flank: one eighth off the front is 1", UWCritterRules.GetFlankingBonus(0, 3), 1);
            fExpectInt("flank: from the side is 2", UWCritterRules.GetFlankingBonus(0, 2), 2);
            fExpectInt("flank: from the other side is 2", UWCritterRules.GetFlankingBonus(0, 6), 2);
            fExpectInt("flank: from behind is 4", UWCritterRules.GetFlankingBonus(0, 0), 4);
            fExpectInt("flank: wraps at 7 to 1", UWCritterRules.GetFlankingBonus(7, 3), 0);

            // The strong individual's bonuses (NPCExecuteAttack label 1679).
            fExpectInt("strong: to-hit at least 7", UWCritterCombat.GetStrongScoreBonus(0), 7);
            fExpectInt("strong: to-hit at most 12", UWCritterCombat.GetStrongScoreBonus(5), 12);
            fExpectInt("strong: damage at least 4", UWCritterCombat.GetStrongDamageBonus(0), 4);
            fExpectInt("strong: damage at most 15", UWCritterCombat.GetStrongDamageBonus(11), 15);

            // The score and damage of an attack from the row: signed to-hit and byte 0x11.
            byte[] lyRow = new byte[48];

            lyRow[5] = 12;
            lyRow[0x11] = 0xFD;
            lyRow[0x13] = 5;
            lyRow[0x14] = 4;
            lyRow[0x16] = 0xFE;
            lyRow[0x17] = 9;

            UWObjectClassProperties.Critter lOKind = new UWObjectClassProperties.Critter { Row = lyRow };

            fExpectInt("attack: score = to-hit + (signed 0x11 >> 1)", UWCritterCombat.GetAttackScore(lOKind, 0), 3);
            fExpectInt("attack: a negative to-hit byte counts", UWCritterCombat.GetAttackScore(lOKind, 1), -4);
            fExpectInt("attack: damage = byte + strength / 5", UWCritterCombat.GetAttackDamage(lOKind, 0), 6);
            fExpectInt("attack: the second attack's damage", UWCritterCombat.GetAttackDamage(lOKind, 1), 11);

            // The critical multiplier ((RNG & 31) + 48) >> 5.
            fExpectInt("critical: low nibble below 16 gives x1", UWCritterCombat.GetCriticalMultiplier(15), 1);
            fExpectInt("critical: 16 gives x2", UWCritterCombat.GetCriticalMultiplier(16), 2);
            fExpectInt("critical: only the low five bits count", UWCritterCombat.GetCriticalMultiplier(32 + 3), 1);

            // The dice: D = max(2, base), (D / 6)d6 + 1d(D % 6), over many rolls within the bounds.
            UWRandom.UseSeed(1220);

            int liMinimum;
            int liMaximum;

            UWCritterCombat.GetDamageBounds(8, out liMinimum, out liMaximum);
            fExpectInt("dice: bounds of 8 are 2..8, low", liMinimum, 2);
            fExpectInt("dice: bounds of 8 are 2..8, high", liMaximum, 8);
            fExpectBool("dice: 400 rolls of 8 stay within 2..8", fRollsWithin(8, 2, 8, 400), true);
            fExpectBool("dice: a base below 2 rolls as 2 (1..2)", fRollsWithin(1, 1, 2, 200), true);
            fExpectBool("dice: 6 is one die (1..6)", fRollsWithin(6, 1, 6, 400), true);
            fExpectBool("dice: 12 is two dice (2..12)", fRollsWithin(12, 2, 12, 400), true);

            UWRandom.Use(null);

            // The charge table seg060 and rolled * charge >> 7 + flank.
            fExpectInt("charge: table starts at 50", UWCritterRules.ChargeTable[0], 50);
            fExpectInt("charge: index 5 is 100", UWCritterRules.ChargeTable[5], 100);
            fExpectInt("charge: table ends at 255", UWCritterRules.ChargeTable[UWCritterRules.ChargeIndexMax], 255);
            fExpectInt("charge: 16 entries", UWCritterRules.ChargeTable.Length, 16);
            fExpectInt("charge: 128 leaves the roll", UWCritterCombat.ScaleByCharge(10, 128, 0), 10);
            fExpectInt("charge: 50 gives 10 * 50 >> 7 = 3", UWCritterCombat.ScaleByCharge(10, 50, 0), 3);
            fExpectInt("charge: 255 gives 19, plus flank 2", UWCritterCombat.ScaleByCharge(10, 255, 2), 21);

            // Missiles: charge 0x80, part + 4, no roll.
            fExpectInt("missile: charge is 0x80", UWCritterRules.MissileCharge, 0x80);
            fExpectInt("missile: body part + 4", UWCritterCombat.GetMissileBodyPart(2), 6);
            fExpectInt("missile: part 7 uses armour byte 3", new UWObjectClassProperties.Critter { Row = new byte[] { 3, 2, 2, 8 } }.ArmourOfPart(7), 8);

            UWRandom.UseSeed(7);
            fExpectBool("missile: damage 6 at charge 0x80 rolls 1..6", fMissileRollsWithin(6, 1, 6, 400), true);
            UWRandom.Use(null);

            // Poison: nibble below byte 0x0F and no resistance, nothing else.
            fExpectBool("poison: 0 below 4 poisons", UWCritterCombat.ShouldPoison(0, 4, 0), true);
            fExpectBool("poison: 3 below 4 poisons", UWCritterCombat.ShouldPoison(3, 4, 0), true);
            fExpectBool("poison: 4 is not below 4", UWCritterCombat.ShouldPoison(4, 4, 0), false);
            fExpectBool("poison: a byte of 0 never poisons", UWCritterCombat.ShouldPoison(0, 0, 0), false);
            fExpectBool("poison: a Poison Resistance rules it out", UWCritterCombat.ShouldPoison(0, 4, UWDamageTypes.Poison), false);
            fExpectInt("poison: writes byte 0x0F & 0xF", UWCritterCombat.GetPoisonNibble(0x14), 4);

            // The armour bytes: row[part % 4], 0xFF falls back to byte 0, strong x 5 / 3.
            UWObjectClassProperties.Critter lOOneValue = new UWObjectClassProperties.Critter { Row = new byte[] { 2, 0xFF, 0xFF, 0xFF } };
            UWObjectClassProperties.Critter lOFourValues = new UWObjectClassProperties.Critter { Row = new byte[] { 3, 2, 2, 8 } };

            fExpectInt("armour: byte 0 is part 0", lOFourValues.ArmourOfPart(0), 3);
            fExpectInt("armour: byte 3 is part 3", lOFourValues.ArmourOfPart(3), 8);
            fExpectInt("armour: part 4 wraps to byte 0", lOFourValues.ArmourOfPart(4), 3);
            fExpectInt("armour: 0xFF in byte 1 falls back to byte 0", lOOneValue.ArmourOfPart(1), 2);
            fExpectInt("armour: 0xFF in byte 3 falls back to byte 0", lOOneValue.ArmourOfPart(3), 2);
            fExpectInt("armour: a strong defender has 3 * 5 / 3 = 5", UWCritterCombat.GetCreatureArmour(lOFourValues, 0, true), 5);
            fExpectInt("armour: a strong defender's 8 becomes 13", UWCritterCombat.GetCreatureArmour(lOFourValues, 3, true), 13);
            fExpectInt("armour: equal to the damage leaves 0", UWCritterCombat.SubtractArmour(5, 5), 0);
            fExpectInt("armour: one above leaves 1", UWCritterCombat.SubtractArmour(6, 5), 1);
            fExpectInt("difficulty: easy halves 5 to 2", UWCritterCombat.HalveOnEasy(5, true), 2);
            fExpectInt("difficulty: standard keeps 5", UWCritterCombat.HalveOnEasy(5, false), 5);

            // The aftermath: effect size, HitZ, blood or flash.
            fExpectInt("effect: size is damage / 4", UWCritterCombat.GetEffectSize(11), 2);
            fExpectInt("effect: size caps at 3", UWCritterCombat.GetEffectSize(16), 3);
            fExpectInt("effect: HitZ of the head is 7", UWCritterCombat.GetHitZ(UWArmourProtection.PartHead), 7);
            fExpectInt("effect: HitZ of a missile part is 0", UWCritterCombat.GetHitZ(4), 0);
            fExpectInt("effect: byte 8 bits 3-4 set bleeds (448)", UWObjectMechanics.GetHitEffectObjectId(0x28), UWObjectMechanics.BloodEffectObjectId);
            fExpectInt("effect: byte 8 bits 3-4 clear flashes (459)", UWObjectMechanics.GetHitEffectObjectId(0x80), UWObjectMechanics.FlashEffectObjectId);
            fExpectInt("effect: blood is class 7 offset 0", UWObjectMechanics.BloodEffectObjectId, 0x1C0);
            fExpectInt("effect: flash is class 7 offset 0x0B", UWObjectMechanics.FlashEffectObjectId, 0x1CB);
            fExpectInt("critical: the off hand for a body hit, right-handed", UWCritterCombat.GetCriticalEquipmentSlot(0, true, 0), 8);
            fExpectInt("critical: the off hand for a hands hit, left-handed", UWCritterCombat.GetCriticalEquipmentSlot(1, false, 0), 7);
            fExpectInt("critical: the helmet for a head hit", UWCritterCombat.GetCriticalEquipmentSlot(3, true, 0), 0);
            fExpectInt("critical: the boots for a leg hit 1 in 5", UWCritterCombat.GetCriticalEquipmentSlot(2, true, 0), 4);
            fExpectInt("critical: else the leggings", UWCritterCombat.GetCriticalEquipmentSlot(2, true, 1), 3);

            // AwardKillEXP: 4 * base + 2d(base), strong x (24 + RNG(24)) / 16, no halving.
            fExpectInt("experience: 4 * 6 + dice 7 = 31", UWCritterCombat.GetKillExperience(6, 7, -1), 31);
            fExpectInt("experience: strong at the low roll is 31 * 24 / 16", UWCritterCombat.GetKillExperience(6, 7, 0), 46);
            fExpectInt("experience: strong at the high roll is 31 * 47 / 16", UWCritterCombat.GetKillExperience(6, 7, 23), 91);
            fExpectInt("experience: base 0 yields nothing", UWCritterCombat.GetKillExperience(0, 2, -1), 0);

            UWRandom.UseSeed(3);
            fExpectBool("experience: 200 rolls for base 6 stay within 26..36", fExperienceWithin(6, 26, 36, 200), true);
            UWRandom.Use(null);

            // EXPChange_seg037_48 (123406-123418): above 96000 tenths nothing is awarded any
            // more - measured against the original 2026-09-20 with a character at 100000.
            UWExperience.State lOAt100000 = fMakeExperienceState(100000, 16, 4, 34);

            fExpectInt("experience gate: nothing above 96000", fChange(ref lOAt100000, 30, 1), 100000);
            fExpectInt("experience gate: no skill point above 96000", lOAt100000.SkillPoints, 4);

            UWExperience.State lOAtCap = fMakeExperienceState(UWExperience.AwardCap, 16, 4, 34);

            fExpectInt("experience gate: exactly at 96000 still counts", fChange(ref lOAtCap, 30, 7), UWExperience.AwardCap + 30);

            UWExperience.State lOShallow = fMakeExperienceState(1000, 16, 0, 0);

            fExpectInt("experience: a level 16 character on dungeon level 1 gets 1 + half",
                fChange(ref lOShallow, 30, 1), 1016);

            UWExperience.State lODeep = fMakeExperienceState(1000, 16, 0, 0);

            fExpectInt("experience: from dungeon level 7 the full amount", fChange(ref lODeep, 30, 7), 1030);

            UWExperience.State lOLoss = fMakeExperienceState(100000, 16, 4, 34);

            fExpectInt("experience: a loss still works above the gate", fChange(ref lOLoss, -1000, 1), 99000);

            // DropNPCRemains: corpse 0xC0 + n with RNG(16) < 7, blood 0xD8 + n.
            fExpectBool("corpse: roll 6 drops", UWCritterCombat.ShouldDropCorpse(6, 6), true);
            fExpectBool("corpse: roll 7 does not", UWCritterCombat.ShouldDropCorpse(6, 7), false);
            fExpectBool("corpse: index 0 never drops", UWCritterCombat.ShouldDropCorpse(0, 0), false);
            fExpectInt("corpse: index 6 is object 0xC6", UWCritterCombat.GetCorpseObjectId(6), 0xC6);
            fExpectInt("corpse: byte 0x0A 0x38 gives index 6", new UWObjectClassProperties.Critter { Row = fRowWith(0x0A, 0x38) }.CorpseIndex, 6);
            fExpectInt("corpse: the quality carries the kind", UWCritterCombat.GetCorpseQuality(0x4E), 0x0E);
            fExpectInt("blood: byte 8 0x28 gives object 217", UWCritterCombat.GetBloodObjectId(new UWObjectClassProperties.Critter { Row = fRowWith(8, 0x28) }.BloodIndex), 217);
            fExpectInt("blood: index 0 gives none", UWCritterCombat.GetBloodObjectId(0), -1);
            fExpectInt("blood: GetRemainsObjectId agrees", UWObjectMechanics.GetRemainsObjectId(0xE8), 0xD8 + 7);
        }

        private static byte[] fRowWith(int piOffset, byte pyValue)
        {
            byte[] lyRow = new byte[48];

            lyRow[piOffset] = pyValue;

            return lyRow;
        }

        private static bool fRollsWithin(int piBase, int piMinimum, int piMaximum, int piCount)
        {
            for (int liRoll = 0; liRoll < piCount; liRoll++)
            {
                int liValue = UWCritterCombat.RollDamage(piBase);

                if (liValue < piMinimum || liValue > piMaximum)
                    return false;
            }

            return true;
        }

        private static bool fMissileRollsWithin(int piAmmoDamage, int piMinimum, int piMaximum, int piCount)
        {
            for (int liRoll = 0; liRoll < piCount; liRoll++)
            {
                int liValue = UWCritterCombat.RollMissileDamage(piAmmoDamage);

                if (liValue < piMinimum || liValue > piMaximum)
                    return false;
            }

            return true;
        }

        /// <summary>A character's experience state for the checks above.</summary>
        private static UWExperience.State fMakeExperienceState(int piExperience, int piLevel,
            int piSkillPoints, int piSkillPointsTotal)
        {
            return new UWExperience.State
            {
                Experience = piExperience,
                Level = piLevel,
                SkillPoints = piSkillPoints,
                SkillPointsTotal = piSkillPointsTotal,
            };
        }

        /// <summary>Applies a change and returns the experience afterwards.</summary>
        private static int fChange(ref UWExperience.State pOState, int piAmount, int piDungeonLevel)
        {
            UWExperience.Change(ref pOState, piAmount, piDungeonLevel);

            return pOState.Experience;
        }

        private static bool fExperienceWithin(int piBase, int piMinimum, int piMaximum, int piCount)
        {
            for (int liRoll = 0; liRoll < piCount; liRoll++)
            {
                int liValue = UWCritterCombat.GetKillExperience(piBase, false);

                if (liValue < piMinimum || liValue > piMaximum)
                    return false;
            }

            return true;
        }

        /// <summary>Spec 7.1, 7.3, 6.3: a collision with the player sets the blocked bit and the
        /// wander step turns a quarter with speed 0; two attackers do not block; one in eight
        /// clears the bit.</summary>
        private static void fCheckBlockedStep()
        {
            FakeCritterHost lOHost = new FakeCritterHost();
            UWCritterRecord lORecord = fMakeRecord(5, 5, 5, 0);
            UWCritterBrain lOBrain = new UWCritterBrain(lORecord, fMakeRow(), 5);

            lOHost.PlacePlayer((8 << 3) + 4, 44);
            lORecord.SetDestination(8, 5, 0);
            lORecord.NewDestination = false;
            lORecord.StraightLineKnown = true;
            lORecord.FineHeading = 64;
            lORecord.FacingEighth = 2;
            lORecord.Speed = 8;
            lORecord.Animation = UWCritterBrain.AnimWalking;
            lOHost.DefaultRoll = 2;
            lOHost.Step = new StepResult { Collided = true, HitObject = true, HitObjectIndex = 1, HitObjectItemId = 0x7F, HitObjectIsCreature = true };
            lOBrain.Update(lOHost);

            fExpectBool("blocked: a collision with the player sets the blocked bit", lORecord.Blocked, true);
            fExpectInt("blocked: the wander step turns a quarter circle", lORecord.FineHeading, 0);
            fExpectInt("blocked: and stands this update", lORecord.Speed, 0);
            fExpectBool("blocked: the straight line is forgotten", lORecord.StraightLineKnown, false);

            lOHost = new FakeCritterHost();
            lORecord = fMakeRecord(5, 5, 5, 0);
            lOBrain = new UWCritterBrain(lORecord, fMakeRow(), 5);
            lOHost.PlacePlayer((8 << 3) + 4, 44);
            lOHost.Objects[7] = new CritterTarget { Exists = true, Index = 7, IsCreature = true, ItemId = 0x45, X = 52, Y = 44, TileX = 6, TileY = 5, HitPoints = 9, Goal = 5 };
            lORecord.SetDestination(8, 5, 0);
            lORecord.NewDestination = false;
            lORecord.StraightLineKnown = true;
            lORecord.Speed = 8;
            lOHost.DefaultRoll = 2;
            lOHost.Step = new StepResult { Collided = true, HitObject = true, HitObjectIndex = 7, HitObjectItemId = 0x45, HitObjectIsCreature = true };
            lOBrain.Update(lOHost);

            fExpectBool("blocked: two attackers bumping do not block", lORecord.Blocked, false);
            fExpectBool("blocked: the bump still drops the straight line and takes the path", lORecord.HasPath, true);
            fExpectInt("blocked: and the walk goes on", lORecord.Animation, UWCritterBrain.AnimWalking);

            lOHost = new FakeCritterHost();
            lORecord = fMakeRecord(5, 5, 5, 0);
            lOBrain = new UWCritterBrain(lORecord, fMakeRow(), 5);
            lOHost.PlacePlayer((8 << 3) + 4, 44);
            lORecord.SetDestination(8, 5, 0);
            lORecord.NewDestination = false;
            lORecord.Blocked = true;
            lORecord.Animation = UWCritterBrain.AnimWalking;
            lOHost.DefaultRoll = 1;
            lOBrain.Update(lOHost);

            fExpectBool("blocked: RNG(8) != 0 keeps the bit", lORecord.Blocked, true);
            fExpectInt("blocked: the reset sends it to goal 2", lORecord.Goal, 2);

            lOHost = new FakeCritterHost();
            lORecord = fMakeRecord(5, 5, 5, 0);
            lOBrain = new UWCritterBrain(lORecord, fMakeRow(), 5);
            lOHost.PlacePlayer((8 << 3) + 4, 44);
            lORecord.SetDestination(8, 5, 0);
            lORecord.NewDestination = false;
            lORecord.Blocked = true;
            lORecord.Animation = UWCritterBrain.AnimWalking;
            lOHost.DefaultRoll = 0;
            lOBrain.Update(lOHost);

            fExpectBool("blocked: RNG(8) == 0 clears the bit", lORecord.Blocked, false);
            fExpectInt("blocked: the wander step walks on", lORecord.Animation, UWCritterBrain.AnimWalking);
            fExpectInt("blocked: goal 5 stays", lORecord.Goal, 5);
        }

        /// <summary>Spec 6.8: goal 10 turns within 0x190 squared eighths and talks within 0x90
        /// when the player looks back.</summary>
        /// <summary>
        /// Goals 13 to 15 are the default case of the goal switch and do nothing but set the
        /// interval (SwitchGoals_seg007_1798_3247, 54050-54056). Pinned on 2026-09-22, when the
        /// Todo's "petrification" turned out to be a label of ours with nothing behind it and
        /// the shipped level data turned out to place no such creature at all.
        /// </summary>
        /// <summary>
        /// The two quest mantras, built 2026-09-22 out of
        /// ChantMantraAtShrine_ovr143_48D label 581 and the direction routine
        /// GetDetectedCreatureDirections_ovr154_11B.
        /// </summary>
        private static void fCheckMantras()
        {
            Console.WriteLine();
            Console.WriteLine("Mantras");

            fExpectInt("mantra: INSAHN is the Cup of Wonder", UWShrineRules.CupOfWonderMantra, 20);
            fExpectInt("mantra: FANLO is the Key of Truth", UWShrineRules.KeyOfTruthMantra, 21);
            fExpectInt("mantra: NO is the empty one", UWShrineRules.EmptyMantra, 22);
            fExpectInt("mantra: the cup lies on tile 24", UWShrineRules.CupTileX, 24);
            fExpectInt("mantra: and on 45", UWShrineRules.CupTileY, 45);
            fExpectInt("mantra: on level three", UWShrineRules.CupLevel, 3);
            fExpectInt("mantra: the key is object 225", UWShrineRules.KeyOfTruthObjectId, 225);

            // The eight directions of GetCardinal_ovr154_7C: 0 north, clockwise, and the Y
            // axis runs north. A straight direction needs one distance to be more than twice
            // the other.
            fExpectInt("mantra: straight north is 0",
                UWShrineRules.GetCardinalDirection(10, 10, 10, 20), 0);
            fExpectInt("mantra: straight east is 2",
                UWShrineRules.GetCardinalDirection(10, 10, 20, 10), 2);
            fExpectInt("mantra: straight south is 4",
                UWShrineRules.GetCardinalDirection(10, 10, 10, 0), 4);
            fExpectInt("mantra: straight west is 6",
                UWShrineRules.GetCardinalDirection(10, 10, 0, 10), 6);
            fExpectInt("mantra: northeast is 1",
                UWShrineRules.GetCardinalDirection(10, 10, 20, 20), 1);
            fExpectInt("mantra: southeast is 3",
                UWShrineRules.GetCardinalDirection(10, 10, 20, 0), 3);
            fExpectInt("mantra: southwest is 5",
                UWShrineRules.GetCardinalDirection(10, 10, 0, 0), 5);
            fExpectInt("mantra: northwest is 7",
                UWShrineRules.GetCardinalDirection(10, 10, 0, 20), 7);

            // The boundary: straight needs half of the one distance to be GREATER than the
            // other, so twice as far is still the diagonal and anything beyond it is straight.
            fExpectInt("mantra: exactly twice as far is still the diagonal",
                UWShrineRules.GetCardinalDirection(0, 0, 20, 10), 1);
            fExpectInt("mantra: one step less and it is straight east",
                UWShrineRules.GetCardinalDirection(0, 0, 20, 9), 2);
        }

        private static void fCheckUnknownGoals()
        {
            Console.WriteLine();
            Console.WriteLine("Goals 13 to 15");

            for (int liGoal = 13; liGoal <= 15; liGoal++)
            {
                FakeCritterHost lOHost = new FakeCritterHost();
                UWCritterRecord lORecord = fMakeRecord(5, 5, liGoal, 0);
                UWCritterBrain lOBrain = new UWCritterBrain(lORecord, fMakeRow(), 5);

                lOHost.PlacePlayer((6 << 3) + 4, (6 << 3) + 4);
                lOBrain.Update(lOHost);

                fExpectInt("goal " + liGoal + ": takes the interval of the default case",
                    lORecord.Interval, UWCritterRules.IntervalUnknownGoal);
                fExpectInt("goal " + liGoal + ": and keeps its goal", lORecord.Goal, liGoal);
                fExpectBool("goal " + liGoal + ": and never starts walking",
                    lORecord.Animation == UWCritterBrain.AnimWalking, false);
                fExpectInt("goal " + liGoal + ": the player right beside it changes nothing",
                    lORecord.Goal, liGoal);
            }
        }

        private static void fCheckTalkGoal()
        {
            FakeCritterHost lOHost = new FakeCritterHost();
            UWCritterRecord lORecord = fMakeRecord(5, 5, 10, 2);
            UWCritterBrain lOBrain = new UWCritterBrain(lORecord, fMakeRow(), 5);

            lOHost.PlacePlayer(44 + 20, 44, 6, 15, 15);
            lOBrain.Update(lOHost);

            fExpectInt("talk: at 0x190 the creature only stands", lORecord.Animation, UWCritterBrain.AnimStanding);
            fExpectInt("talk: and does not turn", lORecord.FacingEighth, 0);

            lOHost.PlacePlayer(44 + 19, 44, 6, 15, 15);
            lOBrain.Update(lOHost);

            fExpectInt("talk: below 0x190 it turns, one eighth per update", lORecord.FacingEighth, 1);
            fExpectBool("talk: but does not speak yet", lOHost.HasEvent("talk"), false);

            lOBrain.Update(lOHost);
            fExpectInt("talk: the second update completes the turn", lORecord.FacingEighth, 2);

            lOHost.PlacePlayer(44 + 12, 44, 6, 15, 15);
            lOBrain.Update(lOHost);
            fExpectBool("talk: at 0x90 it does not speak", lOHost.HasEvent("talk"), false);

            lOHost.PlacePlayer(44 + 11, 44, 2, 15, 15);
            lOBrain.Update(lOHost);
            fExpectBool("talk: below 0x90 with the player looking away it waits", lOHost.HasEvent("talk"), false);

            lOHost.PlacePlayer(44 + 11, 44, 6, 15, 15);
            lOBrain.Update(lOHost);
            fExpectBool("talk: below 0x90 with the player looking back it speaks", lOHost.HasEvent("talk"), true);
            fExpectInt("talk: interval 6 while waiting", lORecord.Interval, 6);
        }

        /// <summary>Spec 3.1, deviation 22: heard while hear2 / 4 &gt; d2 (strict), seen while
        /// d2 &lt;= see^2 with the facing and the sight line, unchanged below hear2 * 4, lost
        /// from there. Loudness 8 and hearing 8 give hear 4; visibility 8 and sight 8 see 4.</summary>
        private static void fCheckPerceptionThresholds()
        {
            FakeCritterHost lOHost;
            UWCritterRecord lORecord;
            UWCritterBrain lOBrain;

            // Heard: d2 3 < 4.
            lOHost = new FakeCritterHost();
            lORecord = fMakeRecord(5, 5, 0, 0);
            lOBrain = new UWCritterBrain(lORecord, fMakeRow(), 5);
            lORecord.FacingEighth = 4;
            lOHost.PlacePlayer((6 << 3) + 4, (6 << 3) + 4);
            lOBrain.Update(lOHost);
            fExpectInt("hearing: d2 2 below hear2 / 4 = 4 is heard, goal 5", lORecord.Goal, 5);

            // Not heard at d2 4 (strict), not seen when facing away, unchanged: heard bit and walk over.
            lOHost = new FakeCritterHost();
            lORecord = fMakeRecord(5, 5, 0, 0);
            lOBrain = new UWCritterBrain(lORecord, fMakeRow(), 5);
            lORecord.FacingEighth = 0;
            lOHost.PlacePlayer((7 << 3) + 4, 44);
            lOBrain.Update(lOHost);
            fExpectInt("hearing: d2 4 equal to hear2 / 4 is not heard", lORecord.Goal, 0);
            fExpectBool("hearing: within hear2 * 4 the heard bit is set", lORecord.HeardSomething, true);
            fExpectInt("hearing: and with the coin it walks over to look", lORecord.Animation, UWCritterBrain.AnimWalking);

            // Seen at d2 4 when facing east.
            lOHost = new FakeCritterHost();
            lORecord = fMakeRecord(5, 5, 0, 0);
            lOBrain = new UWCritterBrain(lORecord, fMakeRow(), 5);
            lORecord.FacingEighth = 2;
            lOHost.PlacePlayer((7 << 3) + 4, 44);
            lOBrain.Update(lOHost);
            fExpectInt("sight: d2 4 facing the player is seen, goal 5", lORecord.Goal, 5);
            fExpectBool("sight: the target bit is set", lORecord.TargetConfirmed, true);

            // Seen at d2 16 (non-strict), one eighth off.
            lOHost = new FakeCritterHost();
            lORecord = fMakeRecord(5, 5, 0, 0);
            lOBrain = new UWCritterBrain(lORecord, fMakeRow(), 5);
            lORecord.FacingEighth = 1;
            lOHost.PlacePlayer((9 << 3) + 4, 44);
            lOBrain.Update(lOHost);
            fExpectInt("sight: d2 16 equal to see^2 is seen, one eighth off the facing", lORecord.Goal, 5);

            // Blocked sight line: not seen, unchanged.
            lOHost = new FakeCritterHost();
            lORecord = fMakeRecord(5, 5, 0, 0);
            lOBrain = new UWCritterBrain(lORecord, fMakeRow(), 5);
            lORecord.FacingEighth = 2;
            lOHost.LineOfSight = false;
            lOHost.PlacePlayer((9 << 3) + 4, 44);
            lOBrain.Update(lOHost);
            fExpectInt("sight: without a sight line it is not seen", lORecord.Goal, 0);

            // d2 25: beyond sight, below hear2 * 4 = 64 -> unchanged.
            lOHost = new FakeCritterHost();
            lORecord = fMakeRecord(5, 5, 0, 0);
            lOBrain = new UWCritterBrain(lORecord, fMakeRow(), 5);
            lORecord.FacingEighth = 2;
            lOHost.PlacePlayer((10 << 3) + 4, 44);
            lOBrain.Update(lOHost);
            fExpectInt("sight: d2 25 beyond see^2 is not seen", lORecord.Goal, 0);
            fExpectBool("sight: but still unchanged, heard bit set", lORecord.HeardSomething, true);

            // d2 64: lost, the target bit cleared.
            lOHost = new FakeCritterHost();
            lORecord = fMakeRecord(5, 5, 0, 0);
            lOBrain = new UWCritterBrain(lORecord, fMakeRow(), 5);
            lORecord.FacingEighth = 2;
            lORecord.TargetConfirmed = false;
            lORecord.HeardSomething = false;
            lOHost.PlacePlayer((13 << 3) + 4, 44);
            lOBrain.Update(lOHost);
            fExpectInt("hearing: d2 64 equal to hear2 * 4 is lost", lORecord.Goal, 0);
            fExpectBool("hearing: lost sets no heard bit", lORecord.HeardSomething, false);
        }

        /// <summary>Spec 6.11: the leash of goal 5 needs the target beyond 2 tiles, backup 4,
        /// bit 5 clear and the creature beyond twice its travel range from home.</summary>
        private static void fCheckLeash()
        {
            FakeCritterHost lOHost;
            UWCritterRecord lORecord;
            UWCritterBrain lOBrain;

            // Home 5/5, travel range 3: leash beyond 4 * 9 = 36 squared tiles, i.e. from 7 tiles.
            lOHost = new FakeCritterHost();
            lORecord = fMakeRecord(12, 5, 5, 0, 5, 5);
            lOBrain = new UWCritterBrain(lORecord, fMakeRow(), 5);
            lORecord.BackupGoal = 4;
            lORecord.TargetConfirmed = true;
            lOHost.PlacePlayer((15 << 3) + 4, 44);
            lOBrain.Update(lOHost);
            fExpectInt("leash: 7 tiles from home with backup 4 gives goal 4", lORecord.Goal, 4);
            fExpectInt("leash: with gtarg 0", lORecord.GTarg, 0);
            fExpectBool("leash: the target bit is cleared", lORecord.TargetConfirmed, false);

            // The destination is the player's tile in the cases that pursue, so the 1-in-8
            // re-search of AttackGoalSearchForTarget (which would reset the goal on "unchanged"
            // with the coin) does not run.
            lOHost = new FakeCritterHost();
            lORecord = fMakeRecord(11, 5, 5, 0, 5, 5);
            lOBrain = new UWCritterBrain(lORecord, fMakeRow(), 5);
            lORecord.BackupGoal = 4;
            lORecord.SetDestination(14, 5, 0);
            lOHost.PlacePlayer((14 << 3) + 4, 44);
            lOBrain.Update(lOHost);
            fExpectInt("leash: at twice the travel range (6 tiles) it still pursues", lORecord.Goal, 5);

            lOHost = new FakeCritterHost();
            lORecord = fMakeRecord(12, 5, 5, 0, 5, 5);
            lOBrain = new UWCritterBrain(lORecord, fMakeRow(), 5);
            lORecord.BackupGoal = 4;
            lORecord.Relentless = true;
            lORecord.SetDestination(15, 5, 0);
            lOHost.PlacePlayer((15 << 3) + 4, 44);
            lOBrain.Update(lOHost);
            fExpectInt("leash: bit 5 set, no leash", lORecord.Goal, 5);

            lOHost = new FakeCritterHost();
            lORecord = fMakeRecord(12, 5, 5, 0, 5, 5);
            lOBrain = new UWCritterBrain(lORecord, fMakeRow(), 5);
            lORecord.SetDestination(15, 5, 0);
            lOHost.PlacePlayer((15 << 3) + 4, 44);
            lOBrain.Update(lOHost);
            fExpectInt("leash: without backup 4, no leash", lORecord.Goal, 5);

            lOHost = new FakeCritterHost();
            lORecord = fMakeRecord(12, 5, 5, 0, 5, 5);
            lOBrain = new UWCritterBrain(lORecord, fMakeRow(), 5);
            lORecord.BackupGoal = 4;
            lOHost.PlacePlayer((13 << 3) + 4, 44);
            lOBrain.Update(lOHost);
            fExpectInt("leash: the target within 2 tiles (0x100 squared eighths), no leash", lORecord.Goal, 5);
        }

        /// <summary>Spec 7.7: the facing turns at most 45 degrees per update; a moving
        /// creature that wants to turn 90 degrees or more stands one update and then takes
        /// the new heading; a 45 degree turn is taken directly; a deflected heading is kept.</summary>
        private static void fCheckTurnLimiter()
        {
            FakeCritterHost lOHost = new FakeCritterHost();
            UWCritterRecord lORecord = fMakeRecord(5, 5, 5, 0);
            UWCritterBrain lOBrain = new UWCritterBrain(lORecord, fMakeRow(), 5);

            lOHost.PlacePlayer((8 << 3) + 4, 44);
            lORecord.Speed = 8;
            lORecord.FineHeading = 0;
            lORecord.FacingEighth = 0;
            lOHost.DefaultRoll = 1;
            lOBrain.Update(lOHost);

            fExpectInt("turn: the facing turns one eighth per update", lORecord.FacingEighth, 1);
            fExpectInt("turn: a 90 degree reversal stops the creature", lORecord.Speed, 0);
            fExpectInt("turn: and keeps the old heading for that update", lORecord.FineHeading, 0);

            lOBrain.Update(lOHost);
            fExpectInt("turn: the next update takes the new heading", lORecord.FineHeading, 64);
            fExpectInt("turn: at full speed", lORecord.Speed, 8);
            fExpectInt("turn: the facing completes the turn", lORecord.FacingEighth, 2);

            lOHost = new FakeCritterHost();
            lORecord = fMakeRecord(5, 5, 5, 0);
            lOBrain = new UWCritterBrain(lORecord, fMakeRow(), 5);
            lOHost.PlacePlayer((8 << 3) + 4, (8 << 3) + 4);
            lORecord.Speed = 8;
            lORecord.FineHeading = 0;
            lOHost.DefaultRoll = 1;
            lOBrain.Update(lOHost);

            fExpectInt("turn: a 45 degree turn is taken directly", lORecord.FineHeading, 32);
            fExpectInt("turn: without stopping", lORecord.Speed, 8);

            lOHost = new FakeCritterHost();
            lORecord = fMakeRecord(5, 5, 5, 0);
            lOBrain = new UWCritterBrain(lORecord, fMakeRow(), 5);
            lOHost.PlacePlayer((8 << 3) + 4, 44);
            lORecord.Speed = 8;
            lORecord.FineHeading = 200;
            lOHost.DefaultRoll = 1;
            lOHost.Step = new StepResult { Collided = true, HeadingDeflected = true };
            lOBrain.Update(lOHost);

            fExpectInt("turn: a deflected heading is kept as the physics left it", lORecord.FineHeading, 200);
            fExpectInt("turn: at the goal's speed", lORecord.Speed, 8);
            fExpectBool("turn: a deflection never sets the blocked bit", lORecord.Blocked, false);
        }

        /// <summary>Spec 4.2: the kin alarm reaches creatures of the same kind within the
        /// hearing range in Manhattan tiles for 0x200 PIT ticks after the player's blow.</summary>
        private static void fCheckKinAlarm()
        {
            FakeCritterHost lOHost = new FakeCritterHost();
            UWCritterRecord lOVictim = fMakeRecord(5, 5, 0, 2);
            UWCritterBrain lOVictimBrain = new UWCritterBrain(lOVictim, fMakeRow(3, 20, 5, 4), 5);

            lOHost.ClockValue = 1000;
            lOHost.PlacePlayer((6 << 3) + 4, 44);
            lOVictimBrain.OnDamaged(lOHost, 1, 2);

            fExpectInt("alarm: the player's blow writes the victim's kind", lOHost.Alarm.Kind, 3);
            fExpectInt("alarm: and its tile", lOHost.Alarm.TileX * 100 + lOHost.Alarm.TileY, 505);

            UWCritterRecord lOKin = fMakeRecord(8, 5, 0, 2);
            UWCritterBrain lOKinBrain = new UWCritterBrain(lOKin, fMakeRow(3, 20, 5, 4), 9);

            // The alarm's destination is the victim's tile, not the player's, so the goal 5
            // routine that follows in the same update would re-search with 1 in 8 (and reset
            // on "unchanged" with the coin): a default roll of 1 keeps that path shut.
            lOHost.ClockValue = 1000 + 0x200;
            lOHost.DefaultRoll = 1;
            lOKinBrain.Update(lOHost);
            fExpectInt("alarm: kin at Manhattan 3 with hearing 4 attacks", lOKin.Goal, 5);
            fExpectBool("alarm: the alarm itself set goal 5", lOHost.HasEvent("goal 0>5"), true);
            fExpectInt("alarm: and turns hostile", lOKin.Attitude, 0);
            fExpectBool("alarm: with the target confirmed", lOKin.TargetConfirmed, true);
            lOHost.DefaultRoll = 0;

            lOKin = fMakeRecord(9, 5, 0, 2);
            lOKinBrain = new UWCritterBrain(lOKin, fMakeRow(3, 20, 5, 4), 9);
            lOKinBrain.Update(lOHost);
            fExpectInt("alarm: kin at Manhattan 4 is out of hearing", lOKin.Goal, 0);

            lOKin = fMakeRecord(8, 5, 0, 2);
            lOKinBrain = new UWCritterBrain(lOKin, fMakeRow(4, 20, 5, 4), 9);
            lOKinBrain.Update(lOHost);
            fExpectInt("alarm: another kind does not care", lOKin.Goal, 0);

            // An ALLY of another kind answers too (53524-53549): hostile, goal 5 at the victim
            // (per user on the original, 2026-09-27: a summoned creature turns hostile when the
            // player hits a creature nearby).
            lOKin = fMakeRecord(8, 5, 0, 2);
            lOKin.IsAlly = true;
            lOKinBrain = new UWCritterBrain(lOKin, fMakeRow(4, 20, 5, 4), 9);
            lOHost.Objects[5] = new CritterTarget
            {
                Exists = true, Index = 5, IsCreature = true, TileX = 5, TileY = 5,
                X = (5 << 3) + 4, Y = (5 << 3) + 4, HitPoints = 20
            };
            lOHost.Events.Clear();
            lOHost.DefaultRoll = 1;
            lOKinBrain.Update(lOHost);
            lOHost.DefaultRoll = 0;
            lOHost.Objects.Remove(5);
            fExpectBool("alarm: an ally of another kind answers (goal 0>5)", lOHost.HasEvent("goal 0>5"), true);
            fExpectInt("alarm: an ally of another kind attacks", lOKin.Goal, 5);
            fExpectInt("alarm: ... the creature the player hit", lOKin.GTarg, 5);
            fExpectInt("alarm: ... and turns hostile", lOKin.Attitude, 0);

            lOKin = fMakeRecord(8, 5, 0, 2);
            lOKinBrain = new UWCritterBrain(lOKin, fMakeRow(3, 20, 5, 4), 9);
            lOHost.ClockValue = 1000 + 0x201;
            lOKinBrain.Update(lOHost);
            fExpectInt("alarm: after 0x200 PIT ticks the alarm is over", lOKin.Goal, 0);

            lOKin = fMakeRecord(8, 5, 0, 2);
            lOKinBrain = new UWCritterBrain(lOKin, fMakeRow(3, 20, 5, 4), 5);
            lOHost.ClockValue = 1000;
            lOKinBrain.Update(lOHost);
            fExpectInt("alarm: the victim itself is not its own kin", lOKin.Goal, 0);
        }


        private static DataImport fCheckLoad(string psDataPath)
        {
            try
            {
                Stopwatch lOWatch = Stopwatch.StartNew();
                DataImport lOData = new DataImport(psDataPath);
                lOWatch.Stop();

                fPass("load", string.Format("{0} ms", lOWatch.ElapsedMilliseconds));

                return lOData;
            }
            catch (Exception lOException)
            {
                fFail("load", lOException.Message);

                return null;
            }
        }

        /// <summary>All nine levels, every tile in place, and everything lying in them known
        /// to COMOBJ.DAT.</summary>
        private static void fCheckLevels(DataImport pOData)
        {
            try
            {
                const int liTilesPerLevel = 64 * 64;

                int liLevels = pOData.Levels == null ? 0 : pOData.Levels.Count;
                int liObjects = 0;
                int liUnknown = 0;
                List<string> lOProblems = new List<string>();

                for (int liAt = 0; pOData.Levels != null && liAt < pOData.Levels.Count; liAt++)
                {
                    UWLevel lOLevel = pOData.Levels[liAt];

                    if (lOLevel == null || lOLevel.TileData == null || lOLevel.TileData.Length != liTilesPerLevel)
                    {
                        lOProblems.Add("level " + (liAt + 1) + " has no complete tile map");

                        continue;
                    }

                    foreach (UWTile lOTile in lOLevel.TileData)
                    {
                        if (lOTile == null)
                        {
                            lOProblems.Add("level " + lOLevel.LevelNumber + " has an empty tile");

                            break;
                        }

                        if (lOTile.ObjectsInTile == null)
                            continue;

                        foreach (UWObject lOObject in lOTile.ObjectsInTile)
                        {
                            if (lOObject == null)
                                continue;

                            liObjects++;

                            if (pOData.CommonObjectProperties != null
                                && !pOData.CommonObjectProperties.TryGet(lOObject.ID, out UWCommonObjectProperties.Entry lOIgnored))
                                liUnknown++;
                        }
                    }
                }

                if (liLevels != 9)
                    lOProblems.Add(liLevels + " levels instead of 9");

                if (liUnknown != 0)
                    lOProblems.Add(liUnknown + " objects without an entry in COMOBJ.DAT");

                if (lOProblems.Count == 0)
                    fPass("levels", string.Format("{0} levels, {1} objects in tiles", liLevels, liObjects));
                else
                    fFail("levels", string.Join("; ", lOProblems));
            }
            catch (Exception lOException)
            {
                fFail("levels", lOException.Message);
            }
        }

        /// <summary>The string blocks the game addresses by number: messages, object and
        /// critter names, conditions, the character screen.</summary>
        private static void fCheckStrings(DataImport pOData)
        {
            try
            {
                if (pOData.Strings == null || pOData.Strings.Blocks == null)
                {
                    fFail("strings", "no string blocks");

                    return;
                }

                int[] liNeeded = { 1, 2, 3, 4, 5, 6, 7 };
                List<string> lOMissing = new List<string>();
                int liStrings = 0;

                foreach (KeyValuePair<int, UWStringBlock> lOPair in pOData.Strings.Blocks)
                    liStrings += lOPair.Value == null || lOPair.Value.Strings == null ? 0 : lOPair.Value.Strings.Count;

                foreach (int liBlock in liNeeded)
                {
                    if (!pOData.Strings.Blocks.TryGetValue(liBlock, out UWStringBlock lOBlock)
                        || lOBlock.Strings == null || lOBlock.Strings.Count == 0)
                        lOMissing.Add(liBlock.ToString());
                }

                // Block 4 holds the object names, one per object id.
                if (pOData.Strings.Blocks.TryGetValue(4, out UWStringBlock lOObjectNames)
                    && lOObjectNames.Strings != null && lOObjectNames.Strings.Count < 465)
                    lOMissing.Add("4 is too short (" + lOObjectNames.Strings.Count + ")");

                if (lOMissing.Count == 0)
                    fPass("strings", string.Format("{0} blocks, {1} strings",
                        pOData.Strings.Blocks.Count, liStrings));
                else
                    fFail("strings", "missing or too short: block " + string.Join(", ", lOMissing));
            }
            catch (Exception lOException)
            {
                fFail("strings", lOException.Message);
            }
        }

        /// <summary>Every picture of the kinds the game draws from, decoded once.</summary>
        private static void fCheckTextures(DataImport pOData)
        {
            UWTexture.TextureTypes[] leTypes =
            {
                UWTexture.TextureTypes.WALL,
                UWTexture.TextureTypes.FLOOR,
                UWTexture.TextureTypes.OBJECTS,
                UWTexture.TextureTypes.DOORS,
                UWTexture.TextureTypes.CURSORS,
                UWTexture.TextureTypes.ANIMO
            };

            try
            {
                int liTextures = 0;
                List<string> lOProblems = new List<string>();

                foreach (UWTexture.TextureTypes leType in leTypes)
                {
                    List<UWTexture> lOTextures = pOData.Textures.GetTexturesByType(leType);

                    if (lOTextures == null || lOTextures.Count == 0)
                    {
                        lOProblems.Add(leType + " is empty");

                        continue;
                    }

                    foreach (UWTexture lOTexture in lOTextures)
                    {
                        // EMPTY ENTRIES ARE NORMAL: the files end in unused slots the game
                        // never draws - three at the end of OBJECTS, one in ANIMO. Only a
                        // picture that claims a size and does not deliver the pixels is broken.
                        if (lOTexture == null || lOTexture.Width <= 0 || lOTexture.Height <= 0)
                            continue;

                        if (lOTexture.PaletteIndices == null
                            || lOTexture.PaletteIndices.Length < lOTexture.Width * lOTexture.Height)
                        {
                            lOProblems.Add(leType + " has a picture without pixels");

                            break;
                        }

                        liTextures++;
                    }
                }

                if (lOProblems.Count == 0)
                    fPass("textures", liTextures + " pictures in " + leTypes.Length + " kinds");
                else
                    fFail("textures", string.Join("; ", lOProblems));
            }
            catch (Exception lOException)
            {
                fFail("textures", lOException.Message);
            }
        }

        /// <summary>
        /// The 3D models are not in a data file but in the game's executable, which is why
        /// only the GOG build is supported: the offsets are build-specific (see
        /// UWGogInstall.SupportedExeSha1).
        /// </summary>
        private static void fCheckModels(string psDataPath)
        {
            try
            {
                string lsExe = DataPath.FindExe(psDataPath);

                if (lsExe == null)
                {
                    fFail("models", "no UW.EXE next to the data folder");

                    return;
                }

                bool lbSupported = UWGogInstall.IsSupportedExe(lsExe);

                UW3DModelImport lOModels = new UW3DModelImport(lsExe);

                int liModels = 0;
                int liFaces = 0;

                for (int liAt = 0; liAt < UW3DModelImport.Count; liAt++)
                {
                    UW3DModel lOModel = lOModels.GetModel(liAt);

                    if (lOModel == null || lOModel.Faces == null || lOModel.Faces.Count == 0)
                        continue;

                    liModels++;
                    liFaces += lOModel.Faces.Count;
                }

                // Counting alone says little - only about half of the 64 slots carry geometry
                // in uw1. What has to be there is what the game actually places.
                string lsMissing = fGetMissingModels(lOModels);

                if (lsMissing != null)
                    fFail("models", "no geometry for: " + lsMissing);
                else if (!lbSupported)
                    fFail("models", string.Format("{0} models with {1} faces, but this UW.EXE is NOT the supported GOG build - colours and models can be wrong",
                        liModels, liFaces));
                else
                    fPass("models", string.Format("{0} models with {1} faces, UW.EXE is the supported build", liModels, liFaces));
            }
            catch (Exception lOException)
            {
                fFail("models", lOException.Message);
            }
        }

        /// <summary>The models the game puts into the world by name. Null if every one of them
        /// has geometry.</summary>
        private static string fGetMissingModels(UW3DModelImport pOModels)
        {
            KeyValuePair<string, int>[] lONeeded =
            {
                new KeyValuePair<string, int>("door frame", UW3DModelImport.ModelIndex.DoorFrame),
                new KeyValuePair<string, int>("bridge", UW3DModelImport.ModelIndex.Bridge),
                new KeyValuePair<string, int>("bench", UW3DModelImport.ModelIndex.Bench),
                new KeyValuePair<string, int>("small boulder", UW3DModelImport.ModelIndex.SmallBoulder),
                new KeyValuePair<string, int>("medium boulder", UW3DModelImport.ModelIndex.MediumBoulder),
                new KeyValuePair<string, int>("large boulder", UW3DModelImport.ModelIndex.LargeBoulder),
                new KeyValuePair<string, int>("arrow", UW3DModelImport.ModelIndex.Arrow),
                new KeyValuePair<string, int>("pillar", UW3DModelImport.ModelIndex.Pillar),
                new KeyValuePair<string, int>("shrine", UW3DModelImport.ModelIndex.Shrine),
                new KeyValuePair<string, int>("gravestone", UW3DModelImport.ModelIndex.Gravestone),
                new KeyValuePair<string, int>("moongate", UW3DModelImport.ModelIndex.Moongate),
                new KeyValuePair<string, int>("chest", UW3DModelImport.ModelIndex.Chest),
                new KeyValuePair<string, int>("nightstand", UW3DModelImport.ModelIndex.Nightstand),
                new KeyValuePair<string, int>("barrel", UW3DModelImport.ModelIndex.Barrel),
                new KeyValuePair<string, int>("chair", UW3DModelImport.ModelIndex.Chair)
            };

            List<string> lOMissing = new List<string>();

            foreach (KeyValuePair<string, int> lOEntry in lONeeded)
            {
                UW3DModel lOModel = pOModels.GetModel(lOEntry.Value);

                if (lOModel == null || lOModel.Faces == null || lOModel.Faces.Count == 0)
                    lOMissing.Add(lOEntry.Key);
            }

            return lOMissing.Count == 0 ? null : string.Join(", ", lOMissing);
        }

        /// <summary>Intro, dreams, end sequence: every animation file of the CUTS folder is
        /// decoded and asked for its frames.</summary>
        private static void fCheckCutscenes(string psDataPath)
        {
            try
            {
                string lsFolder = DataPath.FindSiblingFolder(psDataPath, "CUTS");

                if (lsFolder == null)
                {
                    fFail("cutscenes", "no CUTS folder next to the data folder");

                    return;
                }

                int liFiles = 0;
                int liFrames = 0;
                List<string> lOProblems = new List<string>();

                foreach (string lsFile in Directory.GetFiles(lsFolder, "CS*.N*"))
                {
                    // The .N00 of a sequence is its control script, not an animation.
                    if (lsFile.EndsWith("00", StringComparison.OrdinalIgnoreCase))
                        continue;

                    try
                    {
                        UWCutscene lOCutscene = new UWCutscene(lsFile);

                        if (lOCutscene.FrameCount <= 0 || lOCutscene.Width <= 0 || lOCutscene.Height <= 0)
                        {
                            lOProblems.Add(Path.GetFileName(lsFile) + " has no frames");

                            continue;
                        }

                        // Decoding only happens when a frame is asked for.
                        byte[] lyFrame = lOCutscene.GetFrame(lOCutscene.FrameCount - 1);

                        if (lyFrame == null || lyFrame.Length < lOCutscene.Width * lOCutscene.Height)
                        {
                            lOProblems.Add(Path.GetFileName(lsFile) + " has a short frame");

                            continue;
                        }

                        liFiles++;
                        liFrames += lOCutscene.FrameCount;
                    }
                    catch (Exception lOException)
                    {
                        lOProblems.Add(Path.GetFileName(lsFile) + ": " + lOException.Message);
                    }
                }

                if (lOProblems.Count == 0 && liFiles > 0)
                    fPass("cutscenes", string.Format("{0} files, {1} frames", liFiles, liFrames));
                else
                    fFail("cutscenes", lOProblems.Count == 0
                        ? "no animation files found"
                        : string.Join("; ", lOProblems));
            }
            catch (Exception lOException)
            {
                fFail("cutscenes", lOException.Message);
            }
        }

        /// <summary>The 24 sound effects from SOUNDS.DAT, the AdLib instrument bank and every
        /// music piece the game asks for by number.</summary>
        private static void fCheckSound(DataImport pOData)
        {
            try
            {
                if (pOData.Sound == null || !pOData.Sound.IsAvailable)
                {
                    fFail("sound", "no SOUND folder");

                    return;
                }

                List<string> lOProblems = new List<string>();

                int liEffects = pOData.Sound.Effects == null ? 0 : pOData.Sound.Effects.Count;

                if (liEffects != 24)
                    lOProblems.Add(liEffects + " effects instead of 24");

                if (pOData.Sound.AdlibBank == null)
                    lOProblems.Add("no AdLib instrument bank");

                int liTracks = 0;
                int liNotes = 0;

                foreach (KeyValuePair<string, string> lOTrack in UWSound.MusicTrackNames)
                {
                    UWXmi lOMusic = pOData.Sound.GetMusic(lOTrack.Key, true);

                    if (lOMusic == null || lOMusic.Sequences == null || lOMusic.Sequences.Count == 0)
                    {
                        lOProblems.Add("AW" + lOTrack.Key + " (" + lOTrack.Value + ") has no sequence");

                        continue;
                    }

                    liTracks++;

                    foreach (UWXmi.Sequence lOSequence in lOMusic.Sequences)
                        liNotes += lOSequence.Notes == null ? 0 : lOSequence.Notes.Count;
                }

                if (lOProblems.Count == 0)
                    fPass("sound", string.Format("{0} effects, {1} music pieces with {2} notes",
                        liEffects, liTracks, liNotes));
                else
                    fFail("sound", string.Join("; ", lOProblems));
            }
            catch (Exception lOException)
            {
                fFail("sound", lOException.Message);
            }
        }

        /// <summary>
        /// Why a drowned creature leaves nothing, measured by the user in the original on
        /// 2026-09-20: the death removal runs as after any other death (asm lines 53055-53080
        /// call DropNPCLoot, DropNPCRemains and SpillCritterInventory whatever killed the
        /// creature), and the WATER swallows what it puts down. Three measurements: drowned in
        /// the middle of the water nothing at all is left; drowned close to the shore a piece
        /// of meat stayed lying on the land while what fell into the water was gone, and
        /// without a sound of its own - the single splash of a drowning is the creature's own;
        /// loot of a creature killed at the water's EDGE that falls in splashes on landing.
        ///
        /// What decides in the port is UWCommonObjectProperties.SinksInLiquid (COMOBJ byte 9
        /// below 40, from the user's throwing tests of 2026-08-31), applied in
        /// UWLevelLoader.fSpawnObjectAt - the one place every dropped, thrown, scattered and
        /// spilled object passes. Pinned here for what a death actually puts down.
        /// </summary>
        private static void fCheckWaterSwallowsRemains(DataImport pOData)
        {
            Console.WriteLine();
            Console.WriteLine("Water and what a death leaves");

            if (pOData.CommonObjectProperties == null)
                return;

            // The goblin's row: blood index 5 and corpse index 6 (OBJECTS.DAT byte 8 bits 5-7
            // and byte 0x0A bits 2-4 of critter 7, item id 71 - the creature the user drowned).
            UWObjectClassProperties.Critter lOGoblin;

            fExpectBool("water: the goblin's critter row is there",
                pOData.ObjectClassProperties.TryGetCritter(0x47, out lOGoblin), true);
            fExpectInt("water: the goblin's blood is object 221",
                UWCritterCombat.GetBloodObjectId(lOGoblin.BloodIndex), 0xDD);
            fExpectInt("water: its corpse is object 198",
                UWCritterCombat.GetCorpseObjectId(lOGoblin.CorpseIndex), 0xC6);

            foreach (int liObjectId in new[] { 0xDD, 0xC6, MeatObjectId })
            {
                UWCommonObjectProperties.Entry lOEntry;

                fExpectBool("water: object " + liObjectId + " is in COMOBJ",
                    pOData.CommonObjectProperties.TryGet(liObjectId, out lOEntry), true);
                fExpectBool("water: object " + liObjectId + " sinks, so deep water swallows it",
                    lOEntry.SinksInLiquid, true);
            }

            // The counter-case of the near-shore drowning: the same objects on DRY LAND stay,
            // because only a water or lava tile destroys (UWLevelLoader.IsDestructiveTile).
            fExpectBool("water: nothing sinks on dry land - the tile decides, not the object",
                UWCommonObjectProperties.LiquidSurvivalThreshold > 0, true);

            // THE CULLING, pinned against the user's throwing tests in the original: ten wands
            // and ten emeralds sank, eight of 27 keys stayed (2026-09-21), sixteen stacks of five
            // emeralds all stayed (2026-10-03), and the heavy things of 2026-08-31 stayed. Range
            // 10 to 12, a thrown object tested twice in water. The ids are the port's (one below the string entry):
            // 153 wand, 167 emerald, 258 key, 168 large gem.
            foreach (var lOCase in new[]
            {
                new { Id = 153, Priority = 10, Name = "wand" },
                new { Id = 167, Priority = 11, Name = "emerald" },
                new { Id = 258, Priority = 12, Name = "key" },
                new { Id = 168, Priority = 15, Name = "large gem" }
            })
            {
                UWCommonObjectProperties.Entry lOEntry;

                if (pOData.CommonObjectProperties.TryGet(lOCase.Id, out lOEntry))
                    fExpectInt("culling: the " + lOCase.Name + " has priority " + lOCase.Priority,
                        lOEntry.CullingPriority, lOCase.Priority);
            }

            fExpectInt("culling: the range starts at the callers' 0x0A",
                UWLiquidCulling.RangeBase, 10);
            fExpectInt("culling: a thrown object is tested twice in water",
                UWLiquidCulling.ThrownIntoWaterTests, 2);
            fExpectBool("culling: the wand always goes under",
                UWLiquidCulling.AlwaysSwallows(10), true);
            fExpectBool("culling: the emerald not always",
                UWLiquidCulling.AlwaysSwallows(11), false);
            fExpectBool("culling: the emerald at the lowest range stays",
                UWLiquidCulling.Swallows(11, 1, UWLiquidCulling.RangeBase), false);
            fExpectBool("culling: the key at the middle range stays",
                UWLiquidCulling.Swallows(12, 1, UWLiquidCulling.RangeBase + 1), false);
            fExpectBool("culling: the key at the highest range goes under",
                UWLiquidCulling.Swallows(12, 1, UWLiquidCulling.RangeBase + 2), true);
            fExpectBool("culling: the large gem never goes under",
                UWLiquidCulling.NeverSwallows(15), true);

            // Lava spares quality class 3 and fire-resistant objects before it culls (value 6 of
            // PlacedObjectCollison, read 2026-09-24): Tybal's keys, incense and thread stay,
            // a scroll burns (per user in the original).
            foreach (var lOCase in new[]
            {
                new { Id = 260, Spared = true, Name = "Tybal's first key" },
                new { Id = 278, Spared = true, Name = "block of incense" },
                new { Id = 284, Spared = true, Name = "strong thread" },
                new { Id = 314, Spared = false, Name = "scroll" }
            })
            {
                UWCommonObjectProperties.Entry lOEntry;

                if (pOData.CommonObjectProperties.TryGet(lOCase.Id, out lOEntry))
                    fExpectBool("lava: the " + lOCase.Name + (lOCase.Spared ? " is spared" : " is culled"),
                        UWLiquidCulling.SparedByLava(lOEntry.QualityClass, lOEntry.Resistances), lOCase.Spared);
            }

            fExpectBool("lava: fire resistance alone spares",
                UWLiquidCulling.SparedByLava(0, UWLiquidCulling.FireResistanceBit), true);

            // The stack bonus of ObjectCullingTest: half the EXTRA count counts as priority - a
            // stack of five emeralds is 13 and survives every range (the measurement of
            // 2026-10-03).
            fExpectBool("culling: five of the emerald survive the highest range",
                UWLiquidCulling.Swallows(11, 5, UWLiquidCulling.RangeBase + 2), false);
            fExpectBool("culling: a single one does not",
                UWLiquidCulling.Swallows(11, 1, UWLiquidCulling.RangeBase + 2), true);

            // The second roll of the routine, read out 2026-09-22: it can save nothing at any
            // range the water landing produces, which is why it is not built.
            fExpectInt("culling: the second roll runs over ten values",
                UWLiquidCulling.SecondChanceDice, 10);
            fExpectBool("culling: at the lowest range it already saves nothing",
                UWLiquidCulling.SecondChanceCouldSave(UWLiquidCulling.RangeBase), false);
            fExpectBool("culling: it would only matter below ten",
                UWLiquidCulling.SecondChanceCouldSave(UWLiquidCulling.SecondChanceDice - 1), true);

            // THE SPLASH of something that falls in (built 2026-09-21): class-7 offset 6, so
            // 0x1C6, and string block 4 must name it. The same picture serves the drowning and
            // the falling object - the two places the original spawns it.
            fExpectInt("splash: the picture is object 454",
                UWObjectMechanics.SplashEffectObjectId, 0x1C6);
            fExpectInt("splash: which is the class-7 base plus six",
                UWObjectMechanics.SplashEffectObjectId - UWObjectMechanics.BloodEffectObjectId, 6);

            UWStringBlock lOObjectNames;

            if (pOData.Strings != null && pOData.Strings.Blocks != null
                && pOData.Strings.Blocks.TryGetValue(4, out lOObjectNames)
                && lOObjectNames.Strings != null
                && lOObjectNames.Strings.Count > UWObjectMechanics.SplashEffectObjectId)
            {
                fExpectBool("splash: entry 454 of string block 4 names it",
                    lOObjectNames.Strings[UWObjectMechanics.SplashEffectObjectId]
                        .IndexOf("splash", StringComparison.OrdinalIgnoreCase) >= 0, true);
            }
        }

        /// <summary>"a_piece of meat", the loot that stayed on the land in the user's
        /// near-shore drowning test of 2026-09-20.</summary>
        private const int MeatObjectId = 0xB1;

        /// <summary>
        /// A worn set through the whole status pass, with the real OBJECTS.DAT tables - spec
        /// 8.8. It pins what PlayerStatusUpdate_ovr133_784 would write into the player's row:
        /// the four armour bytes, the protection slots and the weapon skill of the defence.
        /// </summary>
        /// <summary>
        /// The stealth check, built 2026-09-21: the three class-3 spells Stealth, Conceal and
        /// Invisibility become bits 1 to 3 of one accumulator (Class3_Bonuses_ovr133_38E,
        /// label 3BD: 1 shifted by minor class minus one), and the first loop of
        /// ApplyDefenceStealthBonuses_ovr133_65D turns them into the two bases that
        /// ApplyPlayerSneakScore_seg034_2F89_898 writes into the player's row byte 0x1D.
        /// The names come from string block 6 at 16 * major + minor + 1 - 51 Stealth,
        /// 52 Conceal, 53 Invisibilty.
        /// </summary>
        /// <summary>
        /// The easy movement, built 2026-09-21: seg008_1B2A_216 (55427-55876) behind the three
        /// arrows under the compass and, in the original, the uppercase codes of A, D, S, W
        /// and X.
        /// </summary>
        /// <summary>
        /// Music and sound belong to the SAVE, not to the program: PLAYER.DAT 0xB5 carries
        /// both, bits 0-1 the sound effects and bits 2-3 the music, and the loading routine
        /// hands them straight to the two toggles (ovr133 labels 1A4-1C2, 380792-380822; the
        /// writing side is labels 8E-B9). Found on 2026-09-21 after the user tried it in the
        /// original: switch the music off, save, load again and it is still off, while another
        /// save keeps what it was saved with.
        ///
        /// Pinned on the reading side; the writer flips the same two bit pairs with the same
        /// constants.
        /// </summary>
        private static void fCheckSoundInTheSave()
        {
            Console.WriteLine();
            Console.WriteLine("Sound settings in the save");

            // 0x35 is what the original writes into a fresh save: 01 sound, 01 music, 11 detail.
            fExpectBool("sound in save: the fresh 0x35 has the sound on",
                fReadSetting(0x35).SoundEnabled, true);
            fExpectBool("sound in save: and the music on", fReadSetting(0x35).MusicEnabled, true);

            fExpectBool("sound in save: clearing bits 0-1 turns the sound off",
                fReadSetting(0x34).SoundEnabled, false);
            fExpectBool("sound in save: and leaves the music alone",
                fReadSetting(0x34).MusicEnabled, true);

            fExpectBool("sound in save: clearing bits 2-3 turns the music off",
                fReadSetting(0x31).MusicEnabled, false);
            fExpectBool("sound in save: and leaves the sound alone",
                fReadSetting(0x31).SoundEnabled, true);

            fExpectBool("sound in save: the detail bits do not reach either",
                fReadSetting(0x05).SoundEnabled && fReadSetting(0x05).MusicEnabled, true);
        }

        /// <summary>
        /// The volume of the hit sound comes from the damage BEFORE the armour
        /// (AttackerAppliesFinalDamage_seg022_8A5: effect 3 or 4 at label 938, the armour only
        /// at labels A04-A10). Corrected on the player's own blow on 2026-09-22; the
        /// creature's blows had it right since C3.
        /// </summary>
        /// <summary>
        /// The power gem while a blow is charged: one plus the charge over twelve, and with a
        /// RANGED weapon no charge at all - frame 9, the first green one, the moment the
        /// crosshair comes up (seg022_230E_1276 and 1294). Read on 2026-09-22 after the user
        /// had seen the sling jump from red to green in 2026-09-10.
        /// </summary>
        /// <summary>
        /// The impact block of ApplyPlayerMotion_seg008_90D (labels AF3 to B95), read out on
        /// 2026-09-22 because the user could not provoke the wall impact reliably. The answer
        /// is the Acrobat skill.
        /// </summary>
        private static void fCheckImpact()
        {
            Console.WriteLine();
            Console.WriteLine("Impact");

            // A full run is 0x3AC of momentum, so three points - enough to be heard, one short
            // of hurting.
            int liValue = UWImpactRules.GetImpactValue(0x3AC, false);

            fExpectInt("impact: a full run is worth three", liValue, 3);
            fExpectBool("impact: which is heard", UWImpactRules.PlaysSound(liValue, false), true);
            fExpectBool("impact: but does not hurt", UWImpactRules.CausesDamage(liValue), false);
            fExpectInt("impact: at volume three times four less sixty",
                UWImpactRules.GetVolume(liValue), -48);

            fExpectInt("impact: falling doubles it", UWImpactRules.GetImpactValue(0x3AC, true), 6);
            fExpectBool("impact: and that hurts", UWImpactRules.CausesDamage(6), true);

            // Half a run is one point: silent on dry ground, a splash over water.
            fExpectInt("impact: half a run is worth one", UWImpactRules.GetImpactValue(0x1D6, false), 1);
            fExpectBool("impact: one is not heard on dry ground",
                UWImpactRules.PlaysSound(1, false), false);
            fExpectBool("impact: over water it is", UWImpactRules.PlaysSound(1, true), true);
            fExpectBool("impact: and nothing at all is still heard over water",
                UWImpactRules.PlaysSound(0, true), true);

            // THE ACROBAT SKILL is why the user could not provoke it. Henrietta carries 25 in
            // PLAYER.DAT 0x32 in all four saves.
            fExpectInt("impact: a failed check leaves the three whole",
                UWImpactRules.ApplyAcrobat(3, 25, false), 3);
            fExpectInt("impact: Henrietta's Acrobat 25 takes it to nothing",
                UWImpactRules.ApplyAcrobat(3, 25, true), 0);
            fExpectBool("impact: so she hears no wall",
                UWImpactRules.PlaysSound(UWImpactRules.ApplyAcrobat(3, 25, true), false), false);
            fExpectInt("impact: a beginner keeps it even on a passed check",
                UWImpactRules.ApplyAcrobat(3, 0, true), 3);
            fExpectInt("impact: Acrobat 30 takes everything",
                UWImpactRules.ApplyAcrobat(9, 30, true), 0);
            fExpectInt("impact: the check is rolled against twice the value",
                UWImpactRules.GetAcrobatTarget(3), 6);

            // THE JUMP IS THE ANCHOR of the fall: it takes off with 0x263 and comes down at the
            // same speed, so it must sound and must not hurt. That is what our conversion of a
            // fall speed into momentum is calibrated on (UWPlayerMovement.fReportLanding).
            int liJump = UWImpactRules.GetImpactValue(UWImpactRules.JumpMomentum, false);

            fExpectInt("impact: a jump on the spot is worth two", liJump, 2);
            fExpectBool("impact: it is heard", UWImpactRules.PlaysSound(liJump, false), true);
            fExpectBool("impact: and does not hurt", UWImpactRules.CausesDamage(liJump), false);
            fExpectInt("impact: at volume minus fifty-two", UWImpactRules.GetVolume(liJump), -52);
            fExpectBool("impact: a fall of twice that speed does hurt",
                UWImpactRules.CausesDamage(UWImpactRules.GetImpactValue(UWImpactRules.JumpMomentum * 2, false)), true);
        }

        private static void fCheckPowerGem()
        {
            Console.WriteLine();
            Console.WriteLine("Power gem");

            fExpectInt("gem: an uncharged blow is the red frame 1", UWPowerGem.GetFrame(0), 1);
            fExpectInt("gem: the first yellow starts at twelve", UWPowerGem.GetFrame(12), 2);
            fExpectInt("gem: just before it is still red", UWPowerGem.GetFrame(11), 1);
            fExpectInt("gem: the last yellow is frame 8", UWPowerGem.GetFrame(84), 8);
            fExpectInt("gem: green begins at 96", UWPowerGem.GetFrame(96), 9);
            fExpectInt("gem: a full charge is green too", UWPowerGem.GetFrame(UWCombat.FullCharge), 9);

            fExpectInt("gem: a ranged weapon is red until the crosshair",
                UWPowerGem.GetRangedFrame(false), 1);
            fExpectInt("gem: and green from then on, with no yellow in between",
                UWPowerGem.GetRangedFrame(true), 9);
            fExpectInt("gem: which is the same frame a full charge reaches",
                UWPowerGem.GetRangedFrame(true), UWPowerGem.GetFrame(UWCombat.FullCharge));
        }

        private static void fCheckHitVolume()
        {
            Console.WriteLine();
            Console.WriteLine("Hit volume");

            // Armour that swallows everything: no damage, but the sound still has a value.
            int liBefore;
            int liDamage = UWCombat.ComputeDamage(20, false, 30, 0, 128, 1000, out liBefore);

            fExpectInt("hit volume: armour that swallows the blow leaves no damage", liDamage, 0);
            fExpectBool("hit volume: the sound is still loud", liBefore > 0, true);

            // Without armour the two are the same.
            liDamage = UWCombat.ComputeDamage(20, false, 30, 0, 128, 0, out liBefore);

            fExpectInt("hit volume: without armour both are the same", liDamage, liBefore);

            // With armour the difference is exactly the armour.
            liDamage = UWCombat.ComputeDamage(40, false, 30, 0, 128, 3, out liBefore);

            fExpectBool("hit volume: with armour the sound is louder than the damage by it",
                liDamage == 0 || liBefore - liDamage == 3, true);
        }

        /// <summary>A plain PLAYER.DAT block with nothing in it but the settings byte.</summary>
        private static UWPlayerData fReadSetting(int piValue)
        {
            byte[] lyPlain = new byte[320];

            lyPlain[0xB5] = (byte)piValue;

            return UWPlayerData.FromPlain(lyPlain);
        }

        /// <summary>
        /// THE ROLLING EDGES beside the text (UWScrollEdgeRules). Checks the four strips of
        /// SCRLEDGE.GR against the file, the two loop lengths, and how a scroll is counted.
        /// </summary>
        private static void fCheckScrollEdges(DataImport pOData)
        {
            Console.WriteLine();
            Console.WriteLine("Scroll edges");

            // Four strips, and they have to be there.
            fExpectInt("edges: the main screen starts at image 0",
                UWScrollEdgeRules.MainLeftFirstImage, 0);
            fExpectInt("edges: its right strip five later",
                UWScrollEdgeRules.MainRightFirstImage, 5);
            fExpectInt("edges: the main loop runs five frames", UWScrollEdgeRules.MainFrames, 5);
            fExpectInt("edges: the conversation starts at image 10",
                UWScrollEdgeRules.ConversationLeftFirstImage, 10);
            fExpectInt("edges: its right strip six later",
                UWScrollEdgeRules.ConversationRightFirstImage, 16);
            fExpectInt("edges: the conversation loop runs six frames",
                UWScrollEdgeRules.ConversationFrames, 6);

            // The two groups differ in height: 29 on the main screen, 27 in the conversation.
            int liMainHeight = fGetScrledgeHeight(pOData, UWScrollEdgeRules.MainLeftFirstImage);
            int liConvHeight = fGetScrledgeHeight(pOData, UWScrollEdgeRules.ConversationLeftFirstImage);

            fExpectInt("edges: the main strip is 29 pixels high", liMainHeight, 29);
            fExpectInt("edges: the conversation strip is 27", liConvHeight, 27);
            fExpectInt("edges: SCRLEDGE.GR holds 22 images",
                pOData.Textures.GetTexturesByType(UWTexture.TextureTypes.SCRLEDGE).Count, 22);

            // Both loops close: one step past the last frame is the first again.
            fExpectInt("edges: the main loop closes",
                UWScrollEdgeRules.GetMainLeftImage(UWScrollEdgeRules.MainFrames),
                UWScrollEdgeRules.MainLeftFirstImage);
            fExpectInt("edges: the conversation loop closes",
                UWScrollEdgeRules.GetConversationRightImage(UWScrollEdgeRules.ConversationFrames),
                UWScrollEdgeRules.ConversationRightFirstImage);
            fExpectInt("edges: four steps stay inside the main strip",
                UWScrollEdgeRules.GetMainRightImage(4), 9);

            // How we notice a scroll: how far the visible window has moved.
            string[] lsFull = { "a", "b", "c", "d", "e" };
            fExpectInt("edges: nothing moved is no scroll",
                UWScrollEdgeRules.GetScrollAmount(lsFull, lsFull), 0);
            fExpectInt("edges: one line out at the top is one scroll",
                UWScrollEdgeRules.GetScrollAmount(lsFull, new[] { "b", "c", "d", "e", "f" }), 1);
            fExpectInt("edges: three lines at once count three",
                UWScrollEdgeRules.GetScrollAmount(lsFull, new[] { "d", "e", "f", "g", "h" }), 3);
            fExpectInt("edges: a box still filling up does not scroll",
                UWScrollEdgeRules.GetScrollAmount(new[] { "a", "b" }, new[] { "a", "b", "c" }), 0);
            fExpectInt("edges: a cleared box does not turn the strip",
                UWScrollEdgeRules.GetScrollAmount(lsFull, new string[0]), 0);
        }

        private static int fGetScrledgeHeight(DataImport pOData, int piIndex)
        {
            UWTexture lOTexture = pOData.Textures.GetTextureByType(UWTexture.TextureTypes.SCRLEDGE, piIndex);

            return lOTexture == null ? 0 : lOTexture.Height;
        }

        /// <summary>
        /// WHO MINDS A THEFT (UWCritterRules.MindsTheft, AngerNPCByIllegalAction_ovr104_C37) and
        /// what becomes of the owner (ovr104_E4C) - the cases of the disassembly, and the three
        /// messages by attitude from the game's own strings.
        /// </summary>
        private static void fCheckTheft(DataImport pOData)
        {
            Console.WriteLine();
            Console.WriteLine("Theft");

            fExpectBool("theft: a goblin minds the goblins' chest",
                UWCritterRules.MindsTheft(6, 6, false, 0), true);
            fExpectBool("theft: another race does not",
                UWCritterRules.MindsTheft(6, 7, false, 0), false);
            fExpectBool("theft: a locked goblin does not, without bit 5",
                UWCritterRules.MindsTheft(6, 6, true, 0), false);
            fExpectBool("theft: with bit 5 it does",
                UWCritterRules.MindsTheft(0x26, 6, true, 0), true);
            fExpectBool("theft: bit 5 still takes the unlocked ones",
                UWCritterRules.MindsTheft(0x26, 6, false, 0), true);
            fExpectBool("theft: owner 0x20 exactly, only the locked",
                UWCritterRules.MindsTheft(0x20, 0, false, 0), false);
            fExpectBool("theft: owner 0x20 exactly, a locked one of race 0",
                UWCritterRules.MindsTheft(0x20, 0, true, 0), true);
            fExpectBool("theft: the knights mind it while you are none",
                UWCritterRules.MindsTheft(0x0D, 0x0D, false, 2), true);
            fExpectBool("theft: and not once you are one",
                UWCritterRules.MindsTheft(0x0D, 0x0D, false, 3), false);
            fExpectBool("theft: no owner, nobody",
                UWCritterRules.MindsTheft(0, 0, false, 0), false);

            // The area: seven back and eight forward, the far end included (RunCodeOnObjectsInArea).
            fExpectInt("theft: the area reaches seven tiles back", UWCritterRules.TheftAreaBack, 7);
            fExpectInt("theft: and eight forward", UWCritterRules.TheftAreaForward, 8);

            fExpectInt("theft: goodwill drops by one", UWCritterRules.LowerAttitudeForTheft(2), 1);
            fExpectInt("theft: and not below hostile", UWCritterRules.LowerAttitudeForTheft(0), 0);

            fExpectBool("theft: the owner goes up to 0x1B",
                UWCritterRules.ClearsOwnerAfterTheft(0x1B), true);
            fExpectBool("theft: and stays from 0x1C",
                UWCritterRules.ClearsOwnerAfterTheft(0x1C), false);
            // Where and how far a creature looks (AngerNPCByIllegalAction_ovr104_C37): at the item's
            // zpos + height + 12, and the eighths divided by 8 before squaring.
            fExpectInt("theft: sight height of a fish on floor 12", UWCritterRules.GetTheftSightZ(96, 4), 112);
            fExpectBool("theft: seven tiles away in range 7", UWCritterRules.IsWithinTheftSight(56, 0, 7), true);
            fExpectBool("theft: 63 eighths still count as seven tiles", UWCritterRules.IsWithinTheftSight(63, 0, 7), true);
            fExpectBool("theft: eight tiles away out of range 7", UWCritterRules.IsWithinTheftSight(-64, 0, 7), false);
            fExpectBool("theft: five and five is beyond seven", UWCritterRules.IsWithinTheftSight(40, 40, 7), false);

            fExpectBool("theft: the limit looks at the race bits only",
                UWCritterRules.ClearsOwnerAfterTheft(0x3B), true);

            string[] lsExpected = { "angered", "annoyed", "notes" };

            for (int liAttitude = 0; liAttitude < lsExpected.Length; liAttitude++)
            {
                string lsText = pOData.GetGeneralMessage(UWCritterRules.TheftMessageBase + liAttitude) ?? string.Empty;

                fExpectBool("theft: attitude " + liAttitude + " says \"" + lsText.Trim() + "\"",
                    lsText.Contains(lsExpected[liAttitude]), true);
            }
        }

        /// <summary>
        /// THE SIGHT LINE (UWTilePath.TestBetweenPoints, TestBetweenPoints_seg006_1477_1BD1 with
        /// TestTileTraversal_seg006_1056) on a small map made up here: a corridor of three tiles
        /// running east at y 10, floor 2, the line at zpos 40 through the tile centres.
        /// </summary>
        private static void fCheckSightLine()
        {
            Console.WriteLine();
            Console.WriteLine("Sight line");

            const int Z = 40;
            int liX0 = (10 * 8) + 4;
            int liX2 = (12 * 8) + 4;
            int liY = (10 * 8) + 4;

            UWTile[] lOMap = fSightMap(UWTile.TileTypeEnum.open, 2);
            fExpectBool("sight: along an open corridor",
                UWTilePath.TestBetweenPoints(lOMap, liX0, liY, Z, liX2, liY, Z), true);

            lOMap = fSightMap(UWTile.TileTypeEnum.solid, 2);
            fExpectBool("sight: a solid tile in between",
                UWTilePath.TestBetweenPoints(lOMap, liX0, liY, Z, liX2, liY, Z), false);

            // A diagonal tile in the middle: 0x13 (se) closes west and north, 0x15 (sw) east and
            // north - the line east enters by the west and leaves by the east.
            lOMap = fSightMap(UWTile.TileTypeEnum.diagonal_se, 2);
            fExpectBool("sight: into a diagonal by its closed side",
                UWTilePath.TestBetweenPoints(lOMap, liX0, liY, Z, liX2, liY, Z), false);

            lOMap = fSightMap(UWTile.TileTypeEnum.diagonal_sw, 2);
            fExpectBool("sight: out of a diagonal by its closed side",
                UWTilePath.TestBetweenPoints(lOMap, liX0, liY, Z, liX2, liY, Z), false);

            // From the south into the middle tile: se has its south open, nw closes it.
            int liYSouth = (9 * 8) + 4;
            int liXMid = (11 * 8) + 4;

            lOMap = fSightMap(UWTile.TileTypeEnum.diagonal_se, 2);
            lOMap[(9 * 64) + 11] = fSightTile((9 * 64) + 11, UWTile.TileTypeEnum.open, 2);
            fExpectBool("sight: into a diagonal by an open side",
                UWTilePath.TestBetweenPoints(lOMap, liXMid, liYSouth, Z, liXMid, liY, Z), true);

            lOMap = fSightMap(UWTile.TileTypeEnum.diagonal_nw, 2);
            lOMap[(9 * 64) + 11] = fSightTile((9 * 64) + 11, UWTile.TileTypeEnum.open, 2);
            fExpectBool("sight: the same side closed on the other diagonal",
                UWTilePath.TestBetweenPoints(lOMap, liXMid, liYSouth, Z, liXMid, liY, Z), false);

            // A slope closes no side (0x20).
            lOMap = fSightMap(UWTile.TileTypeEnum.slope_e, 2);
            fExpectBool("sight: a slope lets it through",
                UWTilePath.TestBetweenPoints(lOMap, liX0, liY, Z, liX2, liY, Z), true);

            // A floor above the line: zpos 40 is level 5, a floor of 8 stops it.
            lOMap = fSightMap(UWTile.TileTypeEnum.open, 8);
            fExpectBool("sight: a floor higher than the line",
                UWTilePath.TestBetweenPoints(lOMap, liX0, liY, Z, liX2, liY, Z), false);
        }

        /// <summary>A tile as the level loader builds it: type in the low nibble, floor in the
        /// next.</summary>
        private static UWTile fSightTile(int piIndex, UWTile.TileTypeEnum peType, int piFloor)
        {
            return new UWTile(piIndex, (uint)(((piFloor & 0xF) << 4) | ((int)peType & 0xF)), new ushort[64]);
        }

        /// <summary>All solid, the corridor 10..12 / 10 open at floor 2, its middle tile of the
        /// given type and floor.</summary>
        private static UWTile[] fSightMap(UWTile.TileTypeEnum peMiddle, int piMiddleFloor)
        {
            UWTile[] lOMap = new UWTile[64 * 64];

            for (int liAt = 0; liAt < lOMap.Length; liAt++)
                lOMap[liAt] = fSightTile(liAt, UWTile.TileTypeEnum.solid, 0);

            lOMap[(10 * 64) + 10] = fSightTile((10 * 64) + 10, UWTile.TileTypeEnum.open, 2);
            lOMap[(10 * 64) + 11] = fSightTile((10 * 64) + 11, peMiddle, piMiddleFloor);
            lOMap[(10 * 64) + 12] = fSightTile((10 * 64) + 12, UWTile.TileTypeEnum.open, 2);

            return lOMap;
        }

        /// <summary>
        /// WHERE A RESPAWN TRAP FIRES (UWRespawnRules.IsNearPlayer,
        /// CheckIfWithin8TilesFromObject_ovr153_14C4): only when it is NOT near the player -
        /// near meaning fewer than eight tiles on both axes. The player stands on 10/10.
        /// </summary>
        private static void fCheckRespawnDistance(DataImport pOData)
        {
            Console.WriteLine();
            Console.WriteLine("Respawn distance");

            UWTilePos lOPlayer = new UWTilePos(10, 10);

            fExpectBool("respawn: seven tiles east is near", UWRespawnRules.IsNearPlayer(lOPlayer, 17, 10), true);
            fExpectBool("respawn: eight tiles east is not", UWRespawnRules.IsNearPlayer(lOPlayer, 18, 10), false);
            fExpectBool("respawn: eight tiles south is not", UWRespawnRules.IsNearPlayer(lOPlayer, 10, 2), false);
            fExpectBool("respawn: seven on both axes is still near", UWRespawnRules.IsNearPlayer(lOPlayer, 3, 3), true);
            fExpectBool("respawn: the player's own tile is near", UWRespawnRules.IsNearPlayer(lOPlayer, 10, 10), true);

            // The game clock at real time (seg034_2F89_4E adds the PIT ticks per frame): a tick
            // of 21 seconds is 21 * 256 units, 2,394 seconds were 613,021 in the original.
            fExpectInt("clock: one tick is 21 seconds of 256 units", UWGameClock.UnitsPerTick, 5376);
            fExpectInt("clock: a game minute is 60 real seconds", UWGameClock.UnitsPerMinute / UWPlayerTick.PitTicksPerSecond, 60);

            // Tybal's orb (UWTybalOrbRules): level 7 is index 6, and a broken orb stands nowhere.
            bool lbOrbWas = UWGameFlags.OrbDestroyed;
            UWGameFlags.OrbDestroyed = false;
            fExpectBool("orb: stands in the lair, level 7 = index 6", UWTybalOrbRules.OrbStands(6), true);
            fExpectBool("orb: not on level 8 (index 7, the old mistake)", UWTybalOrbRules.OrbStands(7), false);
            UWGameFlags.OrbDestroyed = true;
            fExpectBool("orb: broken, it stands nowhere", UWTybalOrbRules.OrbStands(6), false);
            fExpectInt("orb: Tybal at 120 is left with 61", UWTybalOrbRules.WeakenedHitPoints(120), 61);
            UWGameFlags.OrbDestroyed = lbOrbWas;
            UWCommonObjectProperties.Entry lOOrbRock;
            fExpectBool("orb: the orb rock is used on what it hits when thrown",
                pOData.CommonObjectProperties != null && pOData.CommonObjectProperties.TryGet(UWObjectMechanics.OrbRockObjectId, out lOOrbRock) && lOOrbRock.UsedWhenThrown, true);

            // The head bob (PlayerMotion_seg034_2F89_604): table 1 3 4 3 1 -3 0 0 twice, factor
            // speed * 8 / full - 1, at least 2, nothing at a quarter of the full speed or below.
            fExpectInt("head bob: full speed, top of the step", UWHeadBobRules.GetOffset(32f, 200f, 200f), 28);
            fExpectInt("head bob: full speed, the dip", UWHeadBobRules.GetOffset(80f, 200f, 200f), -21);
            fExpectInt("head bob: slow walk keeps factor 2", UWHeadBobRules.GetOffset(32f, 60f, 200f), 8);
            fExpectInt("head bob: a quarter of the speed does not bob", UWHeadBobRules.GetOffset(32f, 50f, 200f), 0);
            fExpectInt("head bob: two steps a second", UWHeadBobRules.GetOffset(160f, 200f, 200f), 28);
            fExpectInt("head bob: a measured speed above full is capped", UWHeadBobRules.GetOffset(32f, 900f, 200f), 28);
            fExpectInt("head bob smooth: passes through the table at a step", (int)System.Math.Round(UWHeadBobRules.GetSmoothOffset(32f, 200f, 200f)), 28);
            fExpectBool("head bob smooth: between 4 and 3 lies between 28 and 21",
                UWHeadBobRules.GetSmoothOffset(40f, 200f, 200f) < 28f && UWHeadBobRules.GetSmoothOffset(40f, 200f, 200f) > 21f, true);

            // The weapon jitter levels (seg036_3087_245D): speed * 2 / 0x31F + 1 against 0x3AC.
            fExpectInt("weapon jitter: standing is level 0", UWHeadBobRules.GetWeaponJitterLevel(0f), 0);
            fExpectInt("weapon jitter: 40 per cent is level 1", UWHeadBobRules.GetWeaponJitterLevel(0.4f), 1);
            fExpectInt("weapon jitter: 60 per cent is level 2", UWHeadBobRules.GetWeaponJitterLevel(0.6f), 2);
            fExpectInt("weapon jitter: full speed is level 3", UWHeadBobRules.GetWeaponJitterLevel(1f), 3);
            fExpectInt("weapon jitter: level 1 rolls 0 to 4", UWHeadBobRules.GetWeaponJitterRange(1), 5);
            fExpectInt("weapon jitter: level 3 keeps the last", UWHeadBobRules.GetWeaponJitterRange(3), -1);
        }

        /// <summary>
        /// HOW FAR THE AUTOMAP DISCOVERS (UWExplorationRules.IsBrightEnoughToDiscover on the shade of
        /// UWRenderSweep's grid): a drawn cell only below shade 8 of the light level's table with the
        /// doubled distance. Measured against
        /// the original on 2026-09-23 (four runs from the start of level 1): in the dark only the
        /// tile walked on, with a torch (light level 2) also the ring at distance 1.
        /// </summary>
        /// <summary>
        /// THE BOWL AND THE STEW (read 2026-09-28): the container masks as the original tests them
        /// (UWContainerCapacity) and the recipe's mixing (UWStewRules).
        /// </summary>
        private static void fCheckBowlAndStew(DataImport pOData)
        {
            UWObject fItem(int piId)
            {
                return new UWObject((ushort)piId);
            }

            fExpectBool("bowl: takes the dead rotworm",
                UWContainerCapacity.Check(fItem(UWStewRules.BowlObjectId), fItem(0xD9), pOData) == UWContainerCapacity.ResultEnum.Fits, true);
            fExpectBool("bowl: takes a plant (0xCE)",
                UWContainerCapacity.Check(fItem(UWStewRules.BowlObjectId), fItem(0xCE), pOData) == UWContainerCapacity.ResultEnum.Fits, true);
            fExpectBool("bowl: refuses a key (0x100)",
                UWContainerCapacity.Check(fItem(UWStewRules.BowlObjectId), fItem(0x100), pOData) == UWContainerCapacity.ResultEnum.WrongType, true);
            fExpectBool("quiver: refuses the plain stone (0x13)",
                UWContainerCapacity.Check(fItem(0x8D), fItem(0x13), pOData) == UWContainerCapacity.ResultEnum.WrongType, true);
            fExpectBool("quiver: takes an arrow (0x12)",
                UWContainerCapacity.Check(fItem(0x8D), fItem(0x12), pOData) == UWContainerCapacity.ResultEnum.Fits, true);

            UWObject fBowl(params int[] piContents)
            {
                UWObject lOBowl = fItem(UWStewRules.BowlObjectId);

                lOBowl.EnsureContentsLoaded(null);

                foreach (int liId in piContents)
                    lOBowl.Contents.Add(fItem(liId));

                return lOBowl;
            }

            int fMix(UWObject pOBowl)
            {
                UWInventoryModel lOInventory = new UWInventoryModel(() => pOData, () => null);

                lOInventory.Backpack[0] = pOBowl;

                return UWStewRules.TryMix(lOInventory, pOData);
            }

            UWObject lORight = fBowl(0xBE, 0xD9, 0xB8, 0xB8);

            fExpectInt("stew: rotworm, mushroom and port mix", fMix(lORight), UWStewRules.MixedMessage);
            fExpectInt("stew: the bowl becomes the stew", lORight.ID, UWStewRules.RotwormStewObjectId);
            fExpectInt("stew: the ingredients are gone", lORight.Contents.Count, 0);
            fExpectInt("stew: without the port nothing", fMix(fBowl(0xD9, 0xB8)), UWStewRules.WrongIngredientsMessage);
            fExpectInt("stew: a stranger in the bowl spoils it", fMix(fBowl(0xD9, 0xB8, 0xBE, 0xB0)), UWStewRules.WrongIngredientsMessage);
            fExpectInt("stew: no bowl", fMix(null), UWStewRules.NoBowlMessage);

            UWObject lORecipe = fItem(0x13E);

            lORecipe.Quantity = 0x301;
            lORecipe.HasQuantity = true;
            fExpectBool("stew: the scroll with link 0x301 is the recipe", UWStewRules.IsRecipe(lORecipe), true);
            lORecipe.Quantity = 0x220;
            fExpectBool("stew: a scroll with a text below 0x100 is not", UWStewRules.IsRecipe(lORecipe), false);
        }

        /// <summary>UWGlowingRockRules: the glowing rock is not solid, and walked over it joins the
        /// carried one - at the pointer first, else in the inventory; without one nothing.</summary>
        private static void fCheckGlowingRock(DataImport pOData)
        {
            UWObject fRock(int piQuantity)
            {
                UWObject lORock = new UWObject((ushort)UWGlowingRockRules.GlowingRockId);

                lORock.HasQuantity = true;
                lORock.Quantity = (ushort)piQuantity;

                return lORock;
            }

            if (pOData.CommonObjectProperties != null
                && pOData.CommonObjectProperties.TryGet(UWGlowingRockRules.GlowingRockId, out UWCommonObjectProperties.Entry lOEntry))
            {
                fExpectBool("glowing rock: has a height in COMOBJ", lOEntry.Height > 0, true);
                fExpectBool("glowing rock: but is not solid", lOEntry.IsSolid, false);
                fExpectBool("glowing rock: uses itself on whoever touches it", lOEntry.UsedWhenThrown, true);
            }

            UWInventoryModel lOEmpty = new UWInventoryModel(() => pOData, () => null);

            fExpectBool("glowing rock: without a carried one nothing happens",
                UWGlowingRockRules.TryGather(fRock(1), lOEmpty, null), false);

            UWInventoryModel lOInventory = new UWInventoryModel(() => pOData, () => null);
            UWObject lOCarried = fRock(2);

            lOInventory.Backpack[3] = lOCarried;
            fExpectBool("glowing rock: joins the one in the backpack",
                UWGlowingRockRules.TryGather(fRock(3), lOInventory, null), true);
            fExpectInt("glowing rock: the quantities add up", lOCarried.Quantity, 5);

            UWObject lOAtPointer = fRock(1);

            lOInventory.PickUpToCursor(lOAtPointer);
            UWGlowingRockRules.TryGather(fRock(4), lOInventory, null);
            fExpectInt("glowing rock: the one at the pointer takes it first", lOAtPointer.Quantity, 5);
            fExpectInt("glowing rock: the backpack one stays", lOCarried.Quantity, 5);
        }

        /// <summary>UWTrapChainRemoval on the lever of level 3, 52/13 (per user, 2026-10-01): its use
        /// trigger is one-shot, and the original's save after it shows the whole chain gone - the
        /// lever's link 0, the trigger, the three change terrain traps, the text trap and both move
        /// triggers on the free list, the traps out of 44/12, 44/8 and 46/7. A fresh load, as the
        /// check changes the level.</summary>
        private static void fCheckOneShotChainRemoval(string psDataPath)
        {
            DataImport lOData = new DataImport(psDataPath);
            UWLevel lOLevel = lOData.Levels != null && lOData.Levels.Count > 2 ? lOData.Levels[2] : null;
            UWObject lOLever = lOLevel?.GetTile(52, 13)?.ObjectsInTile?.Find(pOAt => pOAt.ID == 0x175);

            if (lOLever == null)
            {
                fFail("one-shot chain: the lever on level 3, 52/13", "not found");
                return;
            }

            List<UWObject> lOMaster = lOLevel.Masterlist;
            UWObject lOTrigger = UWObjectMechanics.GetLinkedObject(lOLever, lOMaster);
            UWObject lOTrap = lOTrigger != null ? UWObjectMechanics.GetLinkedObject(lOTrigger, lOMaster) : null;
            List<int> liChain = new List<int>();

            for (UWObject lOAt = lOTrigger; lOAt != null && liChain.Count < 16; lOAt = UWObjectMechanics.GetLinkedObject(lOAt, lOMaster))
                liChain.Add(lOMaster.IndexOf(lOAt));

            fExpectInt("one-shot chain: trigger, three terrain traps, text trap, two move triggers", liChain.Count, 7);

            UWTrapRules.RemoveOneShotTrap(lOTrigger, lOTrap, lOLevel);

            fExpectInt("one-shot chain: the lever's link is 0", lOLever.Quantity, 0);
            fExpectBool("one-shot chain: every object of it is on the free list",
                liChain.TrueForAll(piAt => lOLevel.IsStaticSlotFree(piAt)), true);

            foreach ((int liX, int liY) in new[] { (44, 12), (44, 8), (46, 7) })
                fExpectBool(string.Format("one-shot chain: no terrain trap left in {0}/{1}", liX, liY),
                    lOLevel.GetTile(liX, liY).ObjectsInTile.Exists(pOAt => pOAt.ID == UWObjectMechanics.ChangeTerrainTrapId), false);
        }

        /// <summary>UWLevel.MarkTileVisited writes a discovered tile again, as seg017_1FDD_DBC does
        /// (per user, 2026-10-01: a lowered water basin stayed water on our map); a marker our host
        /// does not recognise is kept. The byte is put back afterwards.</summary>
        /// <summary>UWGrimeTint (2026-10-07): no tone leaves every index as it is; a tone maps the
        /// ordinary colours to ordinary colours - never to a rotating water or lava index, never to
        /// 0 or the top sixteen - and does change some.</summary>
        private static void fCheckGrimeTint(DataImport pOData)
        {
            UWPalette lOPalette = pOData.Palettes != null ? pOData.Palettes.GetPalette(0) : null;

            if (lOPalette == null)
                return;

            byte[] lyNone = UWGrimeTint.Build(lOPalette, UWGrimeTint.ToneEnum.None);
            bool lbIdentity = true;

            for (int liAt = 0; liAt < lyNone.Length; liAt++)
                lbIdentity &= lyNone[liAt] == liAt;

            fExpectBool("grime tint: none leaves every index", lbIdentity, true);

            byte[] lyDirt = UWGrimeTint.Build(lOPalette, UWGrimeTint.ToneEnum.Dirt);
            bool lbClean = true;
            int liChanged = 0;

            for (int liAt = 1; liAt < 240; liAt++)
            {
                if (UWPaletteRotation.IsRotatingIndex(liAt))
                    continue;

                int liTo = lyDirt[liAt];

                lbClean &= liTo >= 1 && liTo < 240 && !UWPaletteRotation.IsRotatingIndex(liTo);

                if (liTo != liAt)
                    liChanged++;
            }

            fExpectBool("grime tint: dirt stays on ordinary colours", lbClean, true);
            fExpectBool("grime tint: dirt changes colours (" + liChanged + ")", liChanged > 20, true);
        }

        /// <summary>UWPaletteLightMap, the palette renderer's light sources (2026-10-07): a source
        /// falls off by the SHADES.DAT row of its light level, and a solid tile between it and a
        /// point leaves the point dark. On level 1, a tile with a solid one east of it and an open
        /// one beyond.</summary>
        private static void fCheckPaletteLightMap(DataImport pOData)
        {
            UWLevel lOLevel = pOData.Levels != null && pOData.Levels.Count > 0 ? pOData.Levels[0] : null;

            if (lOLevel == null || pOData.Shades == null || !pOData.Shades.IsLoaded)
                return;

            UWPaletteLightMap lOMap = new UWPaletteLightMap();

            lOMap.SetLevel(lOLevel, pOData.Shades);

            byte[] lyRow = pOData.Shades.GetShadeTable(3);

            fExpectInt("light map: light level 3 at its source", (int)Math.Round(lOMap.LevelAt(3f, 0f)), lyRow[0]);
            fExpectInt("light map: light level 3 two tiles off", (int)Math.Round(lOMap.LevelAt(3f, 2f)), lyRow[2]);

            for (int liY = 1; liY < 63; liY++)
            {
                for (int liX = 1; liX < 61; liX++)
                {
                    if (lOLevel.GetTile(liX, liY).TileType != UWTile.TileTypeEnum.open
                        || lOLevel.GetTile(liX + 1, liY).TileType != UWTile.TileTypeEnum.solid
                        || lOLevel.GetTile(liX + 2, liY).TileType != UWTile.TileTypeEnum.open)
                        continue;

                    UWPaletteLightMap.Source[] lOSources =
                    {
                        new UWPaletteLightMap.Source { X = liX, Y = liY, LightLevel = 3f }
                    };

                    lOMap.BuildStatic(lOSources);
                    lOMap.Compose(lOSources);

                    int liPer = UWPaletteLightMap.TexelsPerTile;
                    int liCentre = (liY * liPer) + (liPer / 2);

                    fExpectBool(string.Format("light map: the source's own tile {0}/{1} lit", liX, liY),
                        lOMap.Map[(liCentre * UWPaletteLightMap.Size) + (liX * liPer) + (liPer / 2)] < UWPaletteLightMap.Dark, true);
                    fExpectBool(string.Format("light map: behind the wall at {0}/{1} dark", liX + 2, liY),
                        lOMap.Map[(liCentre * UWPaletteLightMap.Size) + ((liX + 2) * liPer) + (liPer / 2)] == UWPaletteLightMap.Dark, true);
                    return;
                }
            }

            fFail("light map", "no tile with a wall east of it on level 1");
        }

        private static void fCheckMapRewrite(DataImport pOData)
        {
            UWLevel lOLevel = pOData.Levels != null && pOData.Levels.Count > 0 ? pOData.Levels[0] : null;

            if (lOLevel == null)
                return;

            lOLevel.EnsureAutomap();

            int liX = -1, liY = -1;

            for (int liAt = 0; liAt < 4096 && liX < 0; liAt++)
            {
                if (lOLevel.TileData[liAt] != null && lOLevel.TileData[liAt].TileType == UWTile.TileTypeEnum.open)
                {
                    liX = liAt % 64;
                    liY = liAt / 64;
                }
            }

            int liIndex = (liY * 64) + liX;
            byte lyKept = lOLevel.AutomapTiles[liIndex];

            lOLevel.AutomapTiles[liIndex] = 0;
            lOLevel.MarkTileVisited(liX, liY, UWLevel.MapDisplayWater);
            fExpectInt("map rewrite: discovered as water", lOLevel.AutomapTiles[liIndex] >> 4, UWLevel.MapDisplayWater);

            lOLevel.MarkTileVisited(liX, liY, UWLevel.MapDisplayClear);
            fExpectInt("map rewrite: seen again after the change, no longer water", lOLevel.AutomapTiles[liIndex] >> 4, 0);

            lOLevel.AutomapTiles[liIndex] = (byte)((UWLevel.MapDisplayDoor << 4) | 1);
            lOLevel.MarkTileVisited(liX, liY, UWLevel.MapDisplayWater);
            fExpectInt("map rewrite: a door we cannot see is kept, the terrain renewed",
                lOLevel.AutomapTiles[liIndex] >> 4, UWLevel.MapDisplayDoor | UWLevel.MapDisplayWater);

            lOLevel.AutomapTiles[liIndex] = (byte)((UWLevel.MapDisplayStair << 4) | 1);
            lOLevel.MarkTileVisited(liX, liY, UWLevel.MapDisplayWater);
            fExpectInt("map rewrite: a stair the original's rule does not find is cleared",
                lOLevel.AutomapTiles[liIndex] >> 4, UWLevel.MapDisplayWater);

            lOLevel.AutomapTiles[liIndex] = lyKept;

            // The stair marker by the original's rule, a wall picture with a stair texture: the
            // stairs down at 27/20 have one, the water hole into level 2 at 47/53 only a trigger
            // and a teleport (per user, 2026-10-07: black and red specks on our map there).
            UWTileQueries lOQueries = new UWTileQueries(lOLevel, pOData);

            fExpectBool("map stair: level 1 27/20, the stairs down", lOQueries.HasStairMarker(lOLevel.GetTile(27, 20)), true);
            fExpectBool("map stair: level 1 47/53, the water hole, none", lOQueries.HasStairMarker(lOLevel.GetTile(47, 53)), false);
        }

        /// <summary>UWClickRules.CreatureAnswers, see TalkTo_ovr100_0 (per user, 2026-10-01: Oradinar fleeing the
        /// player does not answer in the original).</summary>
        private static void fCheckTalkRefusal()
        {
            fExpectBool("talk: Oradinar withdrawing from the player, upset", UWClickRules.CreatureAnswers(136, 6, 1, 1, false), false);
            fExpectBool("talk: the same, mellow and wandering", UWClickRules.CreatureAnswers(136, 8, 0, 2, false), true);
            fExpectBool("talk: attacking someone else", UWClickRules.CreatureAnswers(136, 5, 7, 1, false), true);
            fExpectBool("talk: hostile", UWClickRules.CreatureAnswers(136, 8, 0, 0, false), false);
            fExpectBool("talk: hostile with goal 10", UWClickRules.CreatureAnswers(136, 10, 1, 0, false), true);
            fExpectBool("talk: an ally attacking the player still answers", UWClickRules.CreatureAnswers(136, 5, 1, 3, true), true);
            fExpectBool("talk: Rodrick always", UWClickRules.CreatureAnswers(0x8E, 5, 1, 0, false), true);
        }

        /// <summary>UWConversationPatches: the trolls' news counter (conversation 288) starts at 0
        /// and the array is written one word on - in the VM's copy only.</summary>
        private static void fCheckConversationPatches(DataImport pOData)
        {
            UWConversations.Conversation lOTrolls = pOData.Conversations != null ? pOData.Conversations.GetConversation(288) : null;

            if (lOTrolls == null || lOTrolls.Code == null || lOTrolls.Code.Length < 927)
            {
                fFail("conversation patch: the generic trolls (288)", "not loaded");
                return;
            }

            ushort[] lyRun = UWConversationPatches.GetCode(lOTrolls);

            fExpectInt("conversation patch: 869 is PUSHI 1 in the original", lOTrolls.Code[870], 1);
            fExpectInt("conversation patch: the counter starts at 0 in the VM", lyRun[870], 0);
            fExpectInt("conversation patch: 895 is PUSHI_EFF 1 in the original", lOTrolls.Code[896], 1);
            fExpectInt("conversation patch: the array is written from local 2 in the VM", lyRun[896], 2);
            fExpectInt("conversation patch: the none-left test stays at 0", lyRun[926], 0);
            fExpectBool("conversation patch: the loaded code is untouched", lyRun != lOTrolls.Code, true);

            // The whole routine, run: one conversation per question, as the troll ends it after
            // the news. Six different ones, then "no new news" with every flag cleared, then again.
            var lOBlock = pOData.Strings.Blocks[lOTrolls.StringBlock];
            int[] liMemory = new int[lOTrolls.MemorySlots];
            List<string> lOHeard = new List<string>();
            bool lbFailed = false;

            for (int liTalk = 0; liTalk < 8; liTalk++)
            {
                ScriptHost lOHost = new ScriptHost();
                UWConversationVM lOVm = new UWConversationVM(lOTrolls, lOHost,
                    piIndex => piIndex + 1 >= 0 && piIndex + 1 < lOBlock.Strings.Count ? lOBlock.Strings[piIndex + 1].Trim() : string.Empty);

                lOVm.LoadMemory(liMemory);

                for (int liStep = 0; liStep < 20; liStep++)
                {
                    UWConversationVM.RunState leState = lOVm.Run();

                    if (leState == UWConversationVM.RunState.Failed)
                        lbFailed = true;

                    if (leState != UWConversationVM.RunState.AwaitingChoice)
                        break;

                    int liChoice = lOVm.PendingChoices.FindIndex(psChoice => psChoice.Contains("news"));

                    lOVm.SupplyChoice((liChoice < 0 ? lOVm.PendingChoices.Count - 1 : liChoice) + 1);
                }

                lOVm.SaveMemory(liMemory);
                lOHeard.Add(lOHost.Said.Count > 0 ? lOHost.Said[lOHost.Said.Count - 1] : string.Empty);
            }

            fExpectBool("troll news: no conversation runs into the endless loop", lbFailed, false);
            fExpectInt("troll news: six different news first", new HashSet<string>(lOHeard.GetRange(0, 6)).Count, 6);
            fExpectBool("troll news: then \"no new news\"", lOHeard[6].StartsWith("I have no new news"), true);
            fExpectBool("troll news: and it starts over", lOHeard[7].Length > 0 && !lOHeard[7].StartsWith("I have no new news"), true);
        }

        /// <summary>UWConversationTextRules.ContainsWord after Contains_ovr093_ED4: the keyword as a
        /// whole word of the typed text, case aside.</summary>
        private static void fCheckContainsWord()
        {
            fExpectBool("contains: the word itself", UWConversationTextRules.ContainsWord("rawstag", "rawstag"), true);
            fExpectBool("contains: any case", UWConversationTextRules.ContainsWord("Tell me of RAWSTAG!", "rawstag"), true);
            fExpectBool("contains: in a sentence", UWConversationTextRules.ContainsWord("the knight, please", "knight"), true);
            fExpectBool("contains: not a part of a word", UWConversationTextRules.ContainsWord("knightly", "knight"), false);
            fExpectBool("contains: not a part typed", UWConversationTextRules.ContainsWord("kn", "knight"), false);
            fExpectBool("contains: a later whole word after a part", UWConversationTextRules.ContainsWord("knights knight", "knight"), true);
            fExpectBool("contains: a digit is no boundary", UWConversationTextRules.ContainsWord("pit2", "pit"), false);
            fExpectBool("contains: empty text", UWConversationTextRules.ContainsWord("", "pit"), false);
        }

        /// <summary>The player deals a fighter's 12 bonus points himself (Str 20, Dex 16, Int 12):
        /// never below the class value, never above 30, done only with none left.</summary>
        private static void fCheckManualAttributes(DataImport pOData)
        {
            UWCharacterGeneration lOGeneration = new UWCharacterGeneration(pOData.Strings, pOData.MiscDataFiles);

            if (!lOGeneration.IsLoaded)
            {
                fFail("manual attributes", "character creation data not loaded");
                return;
            }

            lOGeneration.ManualAttributes = true;
            lOGeneration.Begin();
            lOGeneration.Choose(0);
            lOGeneration.Choose(0);
            lOGeneration.Choose(0);

            fExpectBool("manual attributes: stage after the class", lOGeneration.CurrentStage == UWCharacterGeneration.Stage.Attributes, true);
            fExpectInt("manual attributes: points", lOGeneration.BonusPointsLeft, 12);

            lOGeneration.Choose(6);
            fExpectBool("manual attributes: no done with points left", lOGeneration.CurrentStage == UWCharacterGeneration.Stage.Attributes, true);

            lOGeneration.Choose(1);
            fExpectInt("manual attributes: not below the class value", lOGeneration.Strength, 20);

            for (int liAt = 0; liAt < 11; liAt++)
                lOGeneration.Choose(0);

            fExpectInt("manual attributes: capped at 30", lOGeneration.Strength, 30);
            fExpectInt("manual attributes: points after the cap", lOGeneration.BonusPointsLeft, 2);

            lOGeneration.Choose(1);
            lOGeneration.Choose(4);
            lOGeneration.Choose(4);
            lOGeneration.Choose(4);

            fExpectInt("manual attributes: strength given back", lOGeneration.Strength, 29);
            fExpectInt("manual attributes: intelligence", lOGeneration.Intelligence, 15);
            fExpectInt("manual attributes: none left", lOGeneration.BonusPointsLeft, 0);

            lOGeneration.Choose(6);
            fExpectBool("manual attributes: on to the skills", lOGeneration.CurrentStage == UWCharacterGeneration.Stage.Skill, true);
        }

        /// <summary>Bronus' book opened from the backpack: the sentence, quest flag 8, the curse
        /// with three dice, and the book is gone - a bag beside it stays.</summary>
        private static void fCheckExplodingBook(DataImport pOData)
        {
            UWInventoryModel lOInventory = new UWInventoryModel(() => pOData, () => null);
            UWObject lOBag = new UWObject(130);
            UWObject lOBook = new UWObject((ushort)UWExplodingBook.BookObjectId);
            BookHost lOHost = new BookHost();

            lOInventory.Backpack[0] = lOBag;
            lOInventory.Backpack[3] = lOBook;
            UWQuestFlags.Set(UWExplodingBook.QuestFlag, 0);

            fExpectBool("exploding book: used", UWItemUse.TryUseImmediately(lOBook, pOData, null, lOInventory, lOHost), true);
            fExpectBool("exploding book: the sentence", lOHost.Messages.Contains("The book explodes in your face!"), true);
            fExpectInt("exploding book: quest flag 8", UWQuestFlags.Get(UWExplodingBook.QuestFlag), 1);
            fExpectInt("exploding book: curse dice", lOHost.CurseDice, 3);
            fExpectBool("exploding book: the book is gone", lOHost.Consumed && lOInventory.Backpack[0] == lOBag, true);

            UWQuestFlags.Set(UWExplodingBook.QuestFlag, 0);
        }

        /// <summary>ovr143_95: a Mana gain in Tybal's lair goes into the orb's backup, the flask stays
        /// "0 of 0"; elsewhere, or with the orb broken, into the maximum.</summary>
        private static void fCheckOrbManaAside()
        {
            bool lbWasDestroyed = UWGameFlags.OrbDestroyed;
            int liWasBackup = UWGameFlags.OrbManaBackup;
            int liManaSkill = (int)UWPlayerData.Skill.Mana + 2;

            UWPlayerVitals.StartValues lOStart = UWPlayerVitals.StartValues.Default;

            lOStart.Intelligence = 24;
            lOStart.ManaSkill = 5;

            FakeVitalsHost lOHost = new FakeVitalsHost { DungeonLevelIndex = UWTybalOrbRules.LairLevelIndex };
            UWPlayerVitals lOVitals = new UWPlayerVitals(lOHost);

            lOVitals.Initialise(null, lOStart);
            UWGameFlags.OrbDestroyed = false;
            UWGameFlags.OrbManaBackup = 18;
            lOVitals.SetMana(0f, 0f);

            fExpectBool("orb mana: a Mana gain in the lair", lOVitals.TryIncreaseSkill(liManaSkill), true);
            fExpectInt("orb mana: the maximum stays 0", (int)lOVitals.MaxMana, 0);
            fExpectInt("orb mana: the backup holds the new maximum",
                UWGameFlags.OrbManaBackup, UWExperience.GetMaximumMana(lOVitals.GetSkill(UWPlayerData.Skill.Mana), 24));

            lOHost.DungeonLevelIndex = UWTybalOrbRules.LairLevelIndex - 1;
            lOVitals.TryIncreaseSkill(liManaSkill);
            fExpectInt("orb mana: elsewhere the maximum",
                (int)lOVitals.MaxMana, UWExperience.GetMaximumMana(lOVitals.GetSkill(UWPlayerData.Skill.Mana), 24));

            // Leaving the lair: the maximum from the backup, a quarter of it as current mana.
            lOHost.DungeonLevelIndex = UWTybalOrbRules.LairLevelIndex;
            lOVitals.SetMana(0f, 0f);
            UWGameFlags.OrbManaBackup = 17;
            UWTybalOrbRules.OnLevelChange(lOVitals, UWTybalOrbRules.LairLevelIndex, UWTybalOrbRules.LairLevelIndex - 1);
            fExpectInt("orb mana: leaving the lair, the maximum back", (int)lOVitals.MaxMana, 17);
            fExpectInt("orb mana: leaving the lair, a quarter as current", (int)lOVitals.CurrentMana, 4);

            UWGameFlags.OrbDestroyed = lbWasDestroyed;
            UWGameFlags.OrbManaBackup = liWasBackup;
        }

        /// <summary>SetInteractionMode_seg024_24DC_13D5: drawing the weapon keeps a combat theme,
        /// leaving combat mode keeps it too; Armed and the level theme only from the calm.</summary>
        private static void fCheckWeaponMusic()
        {
            MusicOutput lOOutput = new MusicOutput();
            UWMusicSelector lOSelector = new UWMusicSelector(lOOutput);

            lOSelector.ChangeTheme(UWMusicSelector.EvenCombatTheme);
            lOSelector.Refresh(false, 0f);
            lOSelector.MarkCombat(0f);
            lOSelector.OnWeaponDrawn();
            lOSelector.Refresh(true, 1f);
            fExpectInt("weapon music: drawn during combat music, it goes on", lOSelector.CurrentTheme, UWMusicSelector.EvenCombatTheme);

            lOSelector.OnCombatModeLeft();
            lOSelector.Refresh(false, 2f);
            fExpectInt("weapon music: put away during combat music, it goes on", lOSelector.CurrentTheme, UWMusicSelector.EvenCombatTheme);

            UWMusicSelector lOCalm = new UWMusicSelector(lOOutput);

            lOCalm.ChangeTheme(UWMusicSelector.FirstLevelTheme);
            lOCalm.Refresh(false, 0f);
            lOCalm.OnWeaponDrawn();
            lOCalm.Refresh(true, 1f);
            fExpectInt("weapon music: drawn in the calm, Armed", lOCalm.CurrentTheme, UWMusicSelector.ArmedTheme);

            lOCalm.OnCombatModeLeft();
            lOCalm.Refresh(false, 2f);
            fExpectBool("weapon music: put away from Armed, a level theme",
                lOCalm.CurrentTheme >= UWMusicSelector.FirstLevelTheme && lOCalm.CurrentTheme <= UWMusicSelector.LastLevelTheme, true);

            // RefreshMusic from label 1746: the fanfare ends with nothing requested - a level theme,
            // with the weapon drawn Armed; a request made during the fanfare plays after it.
            MusicOutput lOFanfareOutput = new MusicOutput();
            UWMusicSelector lOFanfare = new UWMusicSelector(lOFanfareOutput);

            lOFanfare.ChangeTheme(UWMusicSelector.FanfareTheme);
            lOFanfare.Refresh(false, 0f);
            lOFanfareOutput.IsPlaying = false;
            lOFanfare.Refresh(false, 1f);
            fExpectBool("music: after the fanfare a level theme",
                lOFanfare.CurrentTheme >= UWMusicSelector.FirstLevelTheme && lOFanfare.CurrentTheme <= UWMusicSelector.LastLevelTheme, true);

            lOFanfareOutput.IsPlaying = true;
            lOFanfare.ChangeTheme(UWMusicSelector.FanfareTheme);
            lOFanfare.Refresh(false, 2f);
            lOFanfare.OnWeaponDrawn();
            lOFanfare.Refresh(true, 3f);
            fExpectInt("music: the fanfare is not interrupted", lOFanfare.CurrentTheme, UWMusicSelector.FanfareTheme);
            lOFanfareOutput.IsPlaying = false;
            lOFanfare.Refresh(true, 4f);
            fExpectInt("music: after the fanfare with the weapon drawn, Armed", lOFanfare.CurrentTheme, UWMusicSelector.ArmedTheme);
        }

        private sealed class MusicOutput : IUWMusicOutput
        {
            public bool IsReady => true;

            public bool IsPlaying { get; set; } = true;

            public void Play(int piTheme) { }

            public void Stop() { }
        }

        private sealed class BookHost : IUWItemUseHost
        {
            public readonly List<string> Messages = new List<string>();
            public int CurseDice;
            public bool Consumed;

            public void ConsumeUsedItem() { Consumed = true; }
            public void AddGeneralMessage(int piIndex) { }
            public void AddMessage(string psMessage) { Messages.Add(psMessage); }
            public void CursePlayer(int piDice) { CurseDice = piDice; }
            public void FlashWindow(int piPaletteColour, float pfSeconds) { }
            public void PlayCutscene(int piCutscene) { }
            public bool CastSpellFromObject(int piSpellIndex) { return false; }
            public bool CastSpellByClass(int piMajorClass, int piMinorClass) { return false; }
            public bool IsWandCoolingDown { get { return false; } }
            public void StartWandCooldown() { }
            public void PlayMagicItemRefused() { }
            public bool TrySleep() { return false; }
            public void SleepPassedOut() { }
            public bool TryPlantSeed() { return false; }
            public bool IsInstrument(int piObjectId) { return false; }
            public bool PlayInstrument(UWObject pOItem) { return false; }
            public bool TryFish() { return false; }
        }

        /// <summary>A conversation host that only listens - for running a script's routine.</summary>
        private sealed class ScriptHost : UWConversationVM.IHost
        {
            public readonly List<string> Said = new List<string>();

            public void Say(string psText) { Said.Add(psText); }

            public void Print(string psText) { }

            public int GetQuest(int piFlag) { return 0; }

            public void SetQuest(int piFlag, int piValue) { }

            public int Random(int piMaximum) { return piMaximum <= 0 ? 0 : UWRandom.Next(1, piMaximum + 1); }

            public int CallUnknown(string psName, int piFunctionId, IReadOnlyList<int> pOArguments,
                IReadOnlyList<int> pOAddresses, UWConversationVM pOVm) { return 0; }
        }

        /// <summary>A walker for the path checks: everything open except the listed wall tiles.</summary>
        private sealed class GridWalker : UWTilePath.IWalker
        {
            public System.Collections.Generic.HashSet<int> Walls = new System.Collections.Generic.HashSet<int>();

            public bool CanEnterTile(int piFromX, int piFromY, int piToX, int piToY)
            {
                return !Walls.Contains((piToY * 64) + piToX);
            }
        }

        private static void fCheckDetectMonsters()
        {
            Console.WriteLine();
            Console.WriteLine("Detect Monsters");

            // DetectMonster_seg038_134E: range 10 (strictly below), skill 0x2D, difficulty 0xF
            // minus the stealth nibble, and the second message above min(most, 3).
            fExpectInt("detect: range is ten", UWMiscSpellRules.DetectRange, 10);
            fExpectInt("track: range is eight (ovr143_0 hands 8 to the same search)", UWMiscSpellRules.TrackRange, 8);
            fExpectInt("detect: stealth 5 gives difficulty 10", UWMiscSpellRules.GetDetectDifficulty(5), 10);
            fExpectInt("detect: no second message for 3 and 3", UWMiscSpellRules.GetSecondDirection(new[] { 3, 0, 3, 0, 0, 0, 0, 0 }, 0, 0), -1);
            fExpectInt("detect: a second message for 4 and 4", UWMiscSpellRules.GetSecondDirection(new[] { 4, 0, 0, 4, 0, 0, 0, 0 }, 0, 0), 3);
            fExpectInt("detect: a second from the random start on", UWMiscSpellRules.GetSecondDirection(new[] { 6, 5, 0, 0, 0, 7, 0, 0 }, 5, 3), 0);
            fExpectInt("detect: never the first direction again", UWMiscSpellRules.GetSecondDirection(new[] { 9, 0, 0, 0, 0, 0, 0, 0 }, 0, 0), -1);
            fExpectInt("detect: one east and three north is north-east, as in the original", UWShrineRules.GetCardinalDirection(0, 0, 1, 3), 1);
            fExpectInt("detect: one step east counts as north-east (integer halves)", UWShrineRules.GetCardinalDirection(0, 0, 1, 0), 1);
        }

        private static void fCheckPoisonSpell()
        {
            Console.WriteLine();
            Console.WriteLine("Poison spell");

            // Poison_seg038_3307_834: 5 dice of 4, type 0x13, no lasting poison.
            // The distance cull: frozen only when far from the player AND from the view.
            fExpectBool("cull: far from both freezes", UWCritterRules.IsFarFromBoth(11, 0, 0, 11), true);
            fExpectBool("cull: near the camera trap's view stays awake", UWCritterRules.IsFarFromBoth(20, 20, 3, 0), false);
            fExpectBool("cull: near the player stays awake", UWCritterRules.IsFarFromBoth(6, 8, 30, 30), false);

            fExpectInt("poison: damage type is poison and magic", UWTargetSpellRules.PoisonDamageType, 0x13);

            // The bounds straight from the lowest and the highest roll of every die. Until
            // 2026-09-26 this rolled 5000 times and waited for 5 and 20 to show up; each needs all
            // five dice at one end (1 in 1024), so a run missed one with about 1.5 % and failed.
            UWRandom.Use(new FixedRandomSource(false));
            int liMin = UWTargetSpellRules.RollPoisonDamage();
            UWRandom.Use(new FixedRandomSource(true));
            int liMax = UWTargetSpellRules.RollPoisonDamage();
            UWRandom.Use(null);

            fExpectInt("poison: at least 5", liMin, 5);
            fExpectInt("poison: at most 20", liMax, 20);
            fExpectInt("poison: a poison-proof creature without magic resistance takes nothing",
                UWDamageTypes.Scale(UWDamageTypes.Poison, 12, UWTargetSpellRules.PoisonDamageType), 0);
            fExpectInt("poison: an ordinary creature takes it all",
                UWDamageTypes.Scale(0, 12, UWTargetSpellRules.PoisonDamageType), 12);
        }

        private static void fCheckSleepCreatures()
        {
            Console.WriteLine();
            Console.WriteLine("Sleep: who keeps the player awake");

            // CheckNearbyNPCHunger_ovr104_6F1: goal 5 or 4, or 9 with the target confirmed.
            fExpectBool("sleep: goal 5 keeps awake", UWSleepRules.BlocksSleep(5, false), true);
            fExpectBool("sleep: goal 4 keeps awake", UWSleepRules.BlocksSleep(4, false), true);
            fExpectBool("sleep: goal 9 unconfirmed does not", UWSleepRules.BlocksSleep(9, false), false);
            fExpectBool("sleep: goal 9 confirmed does", UWSleepRules.BlocksSleep(9, true), true);
            fExpectBool("sleep: a wanderer (goal 8) does not", UWSleepRules.BlocksSleep(8, true), false);
            fExpectInt("sleep: the square before sleeping is two tiles", UWSleepRules.HostileCheckTileRadius, 2);
            fExpectInt("sleep: the ambush square is eight tiles", UWSleepRules.AmbushTileRadius, 8);

            // ovr104_71C: squared distance at most three times the squared travel range.
            fExpectBool("ambush: range 2 reaches 3/1 (10 <= 12)", UWSleepRules.WithinAmbushReach(3, 1, 2), true);
            fExpectBool("ambush: range 2 does not reach 3/2 (13 > 12)", UWSleepRules.WithinAmbushReach(3, 2, 2), false);
            fExpectBool("ambush: range 0 reaches nothing but its own tile", UWSleepRules.WithinAmbushReach(1, 0, 0), false);

            // The path buffer: both ends included, round a wall.
            GridWalker lOWalker = new GridWalker();
            lOWalker.Walls.Add((10 * 64) + 11);
            System.Collections.Generic.List<UWTilePos> lOPath = new System.Collections.Generic.List<UWTilePos>();
            bool lbFound = UWTilePath.TryGetPath(lOWalker, new UWTilePos(10, 10), new UWTilePos(12, 10), lOPath);
            fExpectBool("path: found round the wall", lbFound, true);
            fExpectInt("path: four steps round one wall tile, five entries", lOPath.Count, 5);
            fExpectBool("path: starts at the start and ends at the target",
                lOPath.Count == 5 && lOPath[0] == new UWTilePos(10, 10) && lOPath[4] == new UWTilePos(12, 10), true);

            // ovr104_BB: the spot by tile type, and the north-west quirk.
            int liSx;
            int liSy;
            fExpectBool("ambush spot: open tile 4/4",
                UWSleepRules.TryGetAmbushSpot(UWTile.TileTypeEnum.open, out liSx, out liSy) && liSx == 4 && liSy == 4, true);
            fExpectBool("ambush spot: solid tile refused", UWSleepRules.TryGetAmbushSpot(UWTile.TileTypeEnum.solid, out liSx, out liSy), false);
            UWSleepRules.TryGetAmbushSpot(UWTile.TileTypeEnum.diagonal_ne, out liSx, out liSy);
            fExpectBool("ambush spot: north-east 6/6 lies in the open half",
                UWTileQueries.IsSubTileInOpenHalf(UWTile.TileTypeEnum.diagonal_ne, liSx, liSy), true);
            UWSleepRules.TryGetAmbushSpot(UWTile.TileTypeEnum.diagonal_se, out liSx, out liSy);
            fExpectBool("ambush spot: south-east 6/1 lies in the open half",
                UWTileQueries.IsSubTileInOpenHalf(UWTile.TileTypeEnum.diagonal_se, liSx, liSy), true);
            UWSleepRules.TryGetAmbushSpot(UWTile.TileTypeEnum.diagonal_sw, out liSx, out liSy);
            fExpectBool("ambush spot: south-west 1/1 lies in the open half",
                UWTileQueries.IsSubTileInOpenHalf(UWTile.TileTypeEnum.diagonal_sw, liSx, liSy), true);
            UWSleepRules.TryGetAmbushSpot(UWTile.TileTypeEnum.diagonal_nw, out liSx, out liSy);
            fExpectBool("ambush spot: north-west 6/1 lies in the CLOSED half (the original's table)",
                UWTileQueries.IsSubTileInOpenHalf(UWTile.TileTypeEnum.diagonal_nw, liSx, liSy), false);
        }

        /// <summary>UWLevel.GetActiveMobileOrder (2026-09-29): the list of allocated mobiles at
        /// 0x7AFC, in whose order Tyball's death takes the first creature of each whoami - on
        /// level 7 the 209 guard #222 comes before #223 and #200, as the original removed.</summary>
        private static void fCheckActiveMobileOrder(DataImport pOData)
        {
            Console.WriteLine();
            Console.WriteLine("Allocated mobiles");

            UWLevel lOLevel = pOData.Levels != null && pOData.Levels.Count > 6 ? pOData.Levels[6] : null;

            if (lOLevel == null)
            {
                fExpectBool("mobile order: level 7 loaded", false, true);
                return;
            }

            fExpectBool("mobile order: 209 guard #222 before #223",
                lOLevel.GetActiveMobileOrder(222) < lOLevel.GetActiveMobileOrder(223), true);
            fExpectBool("mobile order: #223 before #200",
                lOLevel.GetActiveMobileOrder(223) < lOLevel.GetActiveMobileOrder(200), true);
            fExpectBool("mobile order: 219 guard #216 before #202",
                lOLevel.GetActiveMobileOrder(216) < lOLevel.GetActiveMobileOrder(202), true);
            fExpectBool("mobile order: 220 guard #197 before #247",
                lOLevel.GetActiveMobileOrder(197) < lOLevel.GetActiveMobileOrder(247), true);
            fExpectInt("mobile order: #197 is the second entry", lOLevel.GetActiveMobileOrder(197), 1);
        }

        /// <summary>UWOriginalRandom (2026-09-29): RNG_seg005_DE7 is Borland's rand, InitRNG sets
        /// the seed. The Gray Goblin #234 of level 1 seeds its barter with its object number: its
        /// threshold nibble 9 gives 54, moved by -2 percent to 53.</summary>
        private static void fCheckOriginalRandom()
        {
            Console.WriteLine();
            Console.WriteLine("Original random generator");

            UWOriginalRandom lORandom = new UWOriginalRandom(1);

            fExpectInt("rng: seed 1, first roll", lORandom.Next(), 346);
            fExpectInt("rng: seed 1, second roll", lORandom.Next(), 130);
            fExpectInt("rng: seed 1, third roll", lORandom.Next(), 10982);

            fExpectInt("rng: goblin #234 threshold 9 * 6 with noise -25 to 25",
                new UWOriginalRandom(234).Noise(54, -25, 25), 53);
        }

        /// <summary>A conversation host that does nothing - for running a few hand-made words.</summary>
        private sealed class SilentConversationHost : UWConversationVM.IHost
        {
            public void Say(string psText) { }
            public void Print(string psText) { }
            public int GetQuest(int piFlag) { return 0; }
            public void SetQuest(int piFlag, int piValue) { }
            public int Random(int piMaximum) { return 1; }

            public int CallUnknown(string psName, int piFunctionId, IReadOnlyList<int> pOArguments,
                IReadOnlyList<int> pOAddresses, UWConversationVM pOVm)
            {
                return 0;
            }
        }

        /// <summary>UWConversationVM PUSHBP/POPBP (2026-09-29): the saved base pointer counts from
        /// the stack, as in UW.EXE. A called function that writes element 0 of a local array
        /// overwrites its caller's saved base pointer with -1 (the generic Gray Goblin's do_offer
        /// function); the caller's locals must stay in the stack, not fall onto play_hp.</summary>
        private static void fCheckConversationBasePointer()
        {
            Console.WriteLine();
            Console.WriteLine("Conversation base pointer");

            const int PlayHpAddress = 4;

            ushort[] lyCode =
            {
                0x22,               //  0 START
                0x13, 5,            //  1 CALL 5
                0x26,               //  3 EXIT_OP
                0x00,               //  4 NOP
                0x1A, 0x1C,         //  5 PUSHBP, SPTOBP - the caller
                0x16, 5, 0x1E,      //  7 PUSHI 5, ADDSP
                0x13, 20,           // 10 CALL 20
                0x17, 5,            // 12 PUSHI_EFF 5 - its fifth local
                0x16, 0, 0x20,      // 14 PUSHI 0, STO
                0x1D, 0x1B, 0x15,   // 17 BPTOSP, POPBP, RET
                0x1A, 0x1C,         // 20 PUSHBP, SPTOBP - the callee
                0x16, 3, 0x1E,      // 22 PUSHI 3, ADDSP
                0x16, 0, 0x17, 1,   // 25 PUSHI 0, PUSHI_EFF 1
                0x21,               // 29 OFFSET - element 0: the saved base pointer
                0x16, 1, 0x29,      // 30 PUSHI 1, OPNEG
                0x20,               // 33 STO
                0x1D, 0x1B, 0x15    // 34 BPTOSP, POPBP, RET
            };

            UWConversations.Conversation lOConversation = new UWConversations.Conversation
            {
                MemorySlots = 35,
                Code = lyCode
            };

            lOConversation.Imports.Add(new UWConversations.Import
            {
                Name = "play_hp",
                IdOrAddress = PlayHpAddress,
                ImportType = UWConversations.VariableImportType
            });

            UWConversationVM lOVm = new UWConversationVM(lOConversation, new SilentConversationHost(), null);

            lOVm.SetImportedGlobal("play_hp", 113);

            UWConversationVM.RunState leState = UWConversationVM.RunState.Running;

            for (int liStep = 0; liStep < 100 && leState == UWConversationVM.RunState.Running; liStep++)
                leState = lOVm.Run();

            fExpectBool("conversation: the hand-made program finishes", leState == UWConversationVM.RunState.Finished, true);
            fExpectInt("conversation: play_hp untouched after a callee wrote -1 on the saved base pointer",
                lOVm.GetImportedGlobal("play_hp"), 113);
        }

        /// <summary>UWEquipmentWear (2026-09-28): the door roll, the weapon and armour tests, and
        /// the dagger of the golem test - quality 15, seven points of wear, 8 left.</summary>
        private static void fCheckEquipmentWear(DataImport pOData)
        {
            Console.WriteLine();
            Console.WriteLine("Equipment wear");

            fExpectBool("wear: a door with id and 7 = 0 never wears the weapon", UWEquipmentWear.WearsWeaponOnDoor(0x140, 0), false);
            fExpectBool("wear: door 0x147 at the highest roll (11 < 14)", UWEquipmentWear.WearsWeaponOnDoor(0x147, 0x7FFF), true);
            fExpectBool("wear: door 0x141 at a roll of 4 (4 < 2 fails)", UWEquipmentWear.WearsWeaponOnDoor(0x141, 0x2AAB), false);
            fExpectBool("wear: door 0x141 at the lowest roll", UWEquipmentWear.WearsWeaponOnDoor(0x141, 0), true);
            fExpectBool("wear: a chest is no door", UWEquipmentWear.WearsWeaponOnDoor(0x15D, 0), false);
            fExpectBool("wear: the dagger is a weapon", UWEquipmentWear.IsWearableWeapon(new UWObject(3)), true);
            fExpectBool("wear: a torch in the weapon hand is not", UWEquipmentWear.IsWearableWeapon(new UWObject(0x95)), false);
            fExpectBool("wear: the shield hand without a shield takes nothing",
                UWEquipmentWear.IsWearableArmour(new UWObject(3), UWArmorItemMap.BodySlot.LeftHandSlot), false);
            fExpectBool("wear: the helmet slot takes what is in it",
                UWEquipmentWear.IsWearableArmour(new UWObject(3), UWArmorItemMap.BodySlot.Helmet), true);
            fExpectInt("wear: savegame slot 8 is the left hand", (int)UWInventoryModel.FromSavegameSlot(8),
                (int)UWArmorItemMap.BodySlot.LeftHandSlot);

            UWObject lODagger = new UWObject(3) { Quality = 15 };

            fExpectInt("wear: the dagger is damaged by 7",
                (int)UWEquipmentWear.Wear(lODagger, 7, pOData, out string lsMessage), (int)UWEquipmentWear.Outcome.Damaged);
            fExpectInt("wear: 15 - 7 leaves 8", lODagger.Quality, 8);
            fExpectBool("wear: Your dagger was damaged.", lsMessage == "Your dagger was damaged.", true);
            fExpectInt("wear: 9 more destroy it",
                (int)UWEquipmentWear.Wear(lODagger, 9, pOData, out lsMessage), (int)UWEquipmentWear.Outcome.Destroyed);
            fExpectBool("wear: Your dagger was destroyed.", lsMessage == "Your dagger was destroyed.", true);
        }

        private static void fCheckMoonstone(DataImport pOData)
        {
            Console.WriteLine();
            Console.WriteLine("Moonstone");

            int liSaved = UWGameFlags.MoonstoneLevel;

            // A bag inside a bag, chained the way the level data are: the container's quantity
            // field points at its first content, the link at the next one.
            UWObject lOStone = new UWObject((ushort)UWMoonstoneRules.MoonstoneId);
            UWObject lOCandle = new UWObject(0x80) { Link = 1 };
            UWObject lOBag = new UWObject(0x80) { Quantity = 2 };
            UWObject lOOuter = new UWObject(0x80) { Quantity = 3 };
            System.Collections.Generic.List<UWObject> lOMaster = new System.Collections.Generic.List<UWObject>
                { new UWObject(0), lOStone, lOCandle, lOBag };
            lOOuter.EnsureContentsLoaded(lOMaster);
            lOBag.EnsureContentsLoaded(lOMaster);

            fExpectBool("moonstone: the stone itself counts", UWMoonstoneRules.IsOrContainsMoonstone(lOStone), true);
            fExpectBool("moonstone: a bag inside a bag holding it counts", UWMoonstoneRules.IsOrContainsMoonstone(lOOuter), true);
            fExpectBool("moonstone: an empty bag does not", UWMoonstoneRules.IsOrContainsMoonstone(new UWObject(0x80)), false);

            UWGameFlags.MoonstoneLevel = 2;
            UWMoonstoneRules.OnTakenFromWorld(lOOuter);
            fExpectInt("moonstone: taken from the world, nobody knows where", UWGameFlags.MoonstoneLevel, 0);
            UWMoonstoneRules.OnPutIntoWorld(lOOuter, 5);
            fExpectInt("moonstone: put down on level 5, Gate Travel goes there", UWGameFlags.MoonstoneLevel, 5);
            UWMoonstoneRules.OnPutIntoWorld(new UWObject(0x80), 3);
            fExpectInt("moonstone: something else put down changes nothing", UWGameFlags.MoonstoneLevel, 5);

            UWGameFlags.MoonstoneLevel = liSaved;

            fExpectBool("moonstone: level 2 holds it at the start",
                pOData.Levels != null && pOData.Levels.Count > 1 && UWMoonstoneRules.FindHolderInLevel(pOData.Levels[1]) != null, true);
        }

        private static void fCheckItemClassSpells(DataImport pOData)
        {
            Console.WriteLine();
            Console.WriteLine("Item spells of class 13");

            // The red potion on level 3 at 49/52 and the wand of the Frog on level 4 at 46/47 -
            // see UWItemSpellRules.
            fExpectInt("item spell: the red potion on level 3 carries Hallucination 13/5",
                fItemClassSpellAt(pOData, 2, 49, 52, 0xBB), 13 * 100 + 5);
            fExpectInt("item spell: the wand on level 4 carries the bullfrog reset 13/3",
                fItemClassSpellAt(pOData, 3, 46, 47, 0x9A), 13 * 100 + 3);

            // A rune spell is none of these: the potion of Lesser Heal form, 512 + 3.
            UWObject lOPotion = new UWObject(0xBB) { HasQuantity = true, IsEnchanted = true, Flags = 4, Quantity = 512 + 3 };
            int liMajor;
            int liMinor;
            fExpectBool("item spell: a rune spell is not a class spell",
                UWObjectMechanics.TryGetItemClassSpell(lOPotion, null, out liMajor, out liMinor), false);
        }

        private static void fCheckScatter()
        {
            Console.WriteLine();
            Console.WriteLine("Scatter around a point");

            int liX;
            int liY;
            int liMinX = int.MaxValue, liMaxX = int.MinValue;

            for (int liRun = 0; liRun < 2000; liRun++)
            {
                UWScatterRules.TryPickSpot(100, 200, UWScatterRules.LootRadius, (x, y) => true, out liX, out liY);
                liMinX = Math.Min(liMinX, liX);
                liMaxX = Math.Max(liMaxX, liX);
            }

            fExpectInt("scatter: loot reaches centre - 6", liMinX, 94);
            fExpectInt("scatter: loot reaches centre + 6", liMaxX, 106);
            fExpectBool("scatter: no spot fits after 24 tries",
                UWScatterRules.TryPickSpot(100, 200, 4, (x, y) => false, out liX, out liY), false);
            fExpectInt("scatter: then the point itself", liX * 1000 + liY, 100200);
        }

        private static void fCheckObjectLimit(DataImport pOData)
        {
            Console.WriteLine();
            Console.WriteLine("Object limit (GetFreeObject, CullObjects)");

            fExpectBool("limit: range 3 spares the player's own tile",
                UWObjectLimitRules.TileQualifies(10, 10, 10, 10, UWObjectLimitRules.CullRange), false);
            fExpectBool("limit: range 3 takes distance 8 (the apples on 50/2, per the original)",
                UWObjectLimitRules.TileQualifies(14, 14, 10, 10, UWObjectLimitRules.CullRange), true);
            fExpectBool("limit: range 3 spares distance 7 (51/2 kept its apples)",
                UWObjectLimitRules.TileQualifies(13, 14, 10, 10, UWObjectLimitRules.CullRange), false);
            fExpectBool("limit: range 3 takes distance 8 in a column too",
                UWObjectLimitRules.TileQualifies(10, 18, 10, 10, UWObjectLimitRules.CullRange), true);

            UWCommonObjectProperties lOProperties = pOData.CommonObjectProperties;
            UWObject lOPotion = new UWObject(0xBB);
            UWObject lOKey = new UWObject(0x100);
            UWObject lOProtected = new UWObject(0xB3) { DoorDirection = true };

            fExpectBool("limit: a potion stays at range 3 (priority 6 - none went in the original)",
                UWObjectLimitRules.Culls(lOPotion, UWObjectLimitRules.CullRange, lOProperties, null), false);
            fExpectBool("limit: a key stays at range 3 (priority 12)",
                UWObjectLimitRules.Culls(lOKey, UWObjectLimitRules.CullRange, lOProperties, null), false);
            fExpectBool("limit: word 0 bit 13 protects",
                UWObjectLimitRules.Culls(lOProtected, UWObjectLimitRules.CullRange, lOProperties, null), false);

            bool lbFits = true;
            string lsCounts = string.Empty;

            for (int liLevel = 0; pOData.Levels != null && liLevel < pOData.Levels.Count; liLevel++)
            {
                int liMobile;
                int liStatic;

                UWObjectLimitRules.CountUsage(pOData.Levels[liLevel], out liMobile, out liStatic);
                lsCounts += string.Format(" {0}:{1}/{2}", liLevel + 1, liMobile, liStatic);
                lbFits &= liMobile <= UWObjectLimitRules.MobileSlots && liStatic <= UWObjectLimitRules.StaticSlots;
            }

            fExpectBool("limit: every fresh level fits its slots (mobile/static)" + lsCounts, lbFits, true);
        }

        private static void fCheckLocks()
        {
            // THE DOOR CAPTURE LIFTS BOTH KINDS (2026-10-06): a door and a portcullis open 24
            // above their closed height and close back onto it - the original's animo raises the
            // grate by the same 0x18 that OpenDoor adds to a door at once (locks-traps notes).
            UWObject lODoor = new UWObject(0x141) { ZPos = 56 };
            UWWorldCapture.CaptureDoor(null, lODoor, true, true, false, 0, 0, null);
            fExpectInt("door capture: an opened door is the open id", lODoor.ID, 0x149);
            fExpectInt("door capture: ... 24 higher", lODoor.ZPos, 80);
            UWWorldCapture.CaptureDoor(null, lODoor, true, false, false, 0, 0, null);
            fExpectInt("door capture: ... and back on the floor when closed", lODoor.ZPos, 56);

            UWObject lOGrate = new UWObject(0x146) { ZPos = 96 };
            UWWorldCapture.CaptureDoor(null, lOGrate, true, true, false, 0, 0, null);
            fExpectInt("door capture: an opened portcullis is 0x14E", lOGrate.ID, 0x14E);
            fExpectInt("door capture: ... 24 higher as well (level 1 tile 9/35: floor 96, grate 120)", lOGrate.ZPos, 120);
            UWWorldCapture.CaptureDoor(null, lOGrate, true, false, false, 0, 0, null);
            fExpectInt("door capture: ... and down again when closed", lOGrate.ZPos, 96);

            Console.WriteLine();
            Console.WriteLine("Locks (UnlockDoor)");

            // A chest with a locked lock of id 5, difficulty 0, without the keep bit.
            UWObject lOLock = new UWObject((ushort)UWObjectMechanics.LockObjectId) { Flags = 1, Quantity = 5 };
            UWObject lOChest = new UWObject(0x15D) { Quantity = 1 };
            lOChest.EnsureContentsLoaded(new System.Collections.Generic.List<UWObject> { new UWObject(0), lOLock });

            fExpectInt("lock: a pick of skill 30 opens difficulty 0",
                (int)UWLockRules.Unlock(null, lOChest, lOLock, false, -30), (int)UWLockRules.Outcome.Unlocked);
            fExpectBool("lock: without the keep bit the lock is gone", lOChest.Contents.Contains(lOLock), false);

            UWObject lOKept = new UWObject((ushort)UWObjectMechanics.LockObjectId) { Flags = 3, Quantity = 5 };
            UWObject lOBarrel = new UWObject(0x15D) { Quantity = 1 };
            lOBarrel.EnsureContentsLoaded(new System.Collections.Generic.List<UWObject> { new UWObject(0), lOKept });

            fExpectInt("lock: a wrong key does not fit",
                (int)UWLockRules.Unlock(null, lOBarrel, lOKept, false, 4), (int)UWLockRules.Outcome.Failed);
            fExpectInt("lock: the right key unlocks",
                (int)UWLockRules.Unlock(null, lOBarrel, lOKept, false, 5), (int)UWLockRules.Outcome.Unlocked);
            fExpectBool("lock: with the keep bit the lock stays", lOBarrel.Contents.Contains(lOKept), true);
            fExpectInt("lock: a pick on an unlocked lock is 'not locked'",
                (int)UWLockRules.Unlock(null, lOBarrel, lOKept, false, -30), (int)UWLockRules.Outcome.NotLocked);
            fExpectInt("lock: the key does not lock an open container",
                (int)UWLockRules.Unlock(null, lOBarrel, lOKept, true, 5), (int)UWLockRules.Outcome.NotLocked);
            fExpectInt("lock: the key locks it again",
                (int)UWLockRules.Unlock(null, lOBarrel, lOKept, false, 5), (int)UWLockRules.Outcome.Locked);

            UWObject lOHard = new UWObject((ushort)UWObjectMechanics.LockObjectId) { Flags = 3, Quantity = 0, ZPos = 0xF };
            fExpectInt("lock: difficulty 15 withstands even the Unlock spell",
                (int)UWLockRules.Unlock(null, lOBarrel, lOHard, false, -UWLockRules.UnlockSpellSkill), (int)UWLockRules.Outcome.Failed);
            fExpectInt("lock: a lock of id 0 takes no key",
                (int)UWLockRules.Unlock(null, lOBarrel, lOHard, false, 1), (int)UWLockRules.Outcome.Failed);
        }

        /// <summary>Major * 100 + minor of the first object of that id in the tile, or -1.</summary>
        private static int fItemClassSpellAt(DataImport pOData, int piLevel, int piX, int piY, int piId)
        {
            UWLevel lOLevel = pOData.Levels != null && pOData.Levels.Count > piLevel ? pOData.Levels[piLevel] : null;
            UWTile lOTile = lOLevel != null ? lOLevel.GetTile(piX, piY) : null;

            if (lOTile == null || lOTile.ObjectsInTile == null)
                return -1;

            foreach (UWObject lOObject in lOTile.ObjectsInTile)
            {
                int liMajor;
                int liMinor;

                if (lOObject != null && lOObject.ID == piId
                    && UWObjectMechanics.TryGetItemClassSpell(lOObject, lOLevel.Masterlist, out liMajor, out liMinor))
                    return liMajor * 100 + liMinor;
            }

            return -1;
        }

        /// <summary>The shade test for one cell of the grid, the shades alone (no level: the sweep
        /// draws nothing, its shade grid is filled all the same).</summary>
        private static bool fIsBright(byte[] pyShadeTable, int piRow, int piColumn)
        {
            UWRenderSweep lOSweep = new UWRenderSweep();

            lOSweep.Run(null, 0, 0, 0, pyShadeTable);

            return UWExplorationRules.IsBrightEnoughToDiscover(lOSweep.GetShade(piRow, piColumn));
        }

        private static void fCheckMapReveal(DataImport pOData)
        {
            Console.WriteLine();
            Console.WriteLine("Map reveal");

            byte[] lyDark = pOData.Shades != null ? pOData.Shades.GetShadeTable(0, true) : null;
            byte[] lyTorch = pOData.Shades != null ? pOData.Shades.GetShadeTable(2, true) : null;
            byte[] lyBright = pOData.Shades != null ? pOData.Shades.GetShadeTable(7, true) : null;

            if (lyDark == null || lyTorch == null || lyBright == null)
            {
                fFail("map reveal", "SHADES.DAT not loaded");
                return;
            }

            fExpectBool("map reveal: dark, the own tile", fIsBright(lyDark, 0, 0), true);
            fExpectBool("map reveal: dark, not the tile ahead", fIsBright(lyDark, 1, 0), false);
            fExpectBool("map reveal: torch, the tile ahead", fIsBright(lyTorch, 1, 0), true);
            fExpectBool("map reveal: torch, diagonal ahead", fIsBright(lyTorch, 1, 1), true);
            fExpectBool("map reveal: torch, not two ahead", fIsBright(lyTorch, 2, 0), false);
            fExpectBool("map reveal: torch, not two aside", fIsBright(lyTorch, 0, 2), false);
            fExpectBool("map reveal: brightest, two ahead", fIsBright(lyBright, 2, 0), true);
            fExpectBool("map reveal: brightest, not three ahead", fIsBright(lyBright, 3, 0), false);

            // THE ORIGINAL'S SWEEP (UWRenderSweep, seg031_121A). Positions in the original's units,
            // the tile centre 128/128.
            UWLevel lOLevel = pOData.Levels != null && pOData.Levels.Count > 0 ? pOData.Levels[0] : null;
            UWLevel lOLevel6 = pOData.Levels != null && pOData.Levels.Count > 5 ? pOData.Levels[5] : null;

            if (lOLevel == null || lOLevel6 == null)
            {
                fFail("map reveal", "levels 1 and 6 not loaded");
                return;
            }

            UWRenderSweep lOSweep = new UWRenderSweep();

            // Level 6, 5/54 looking north with a lantern (per user, 2026-10-01, measured against
            // the original): ahead the diagonal 5/55 turns its closed back, so the view ends in the
            // own row - and in that row the original discovers the diagonal 4/54 beside the player,
            // whose open side faces him. The grid line of sight before the sweep missed it.
            lOSweep.Run(lOLevel6, (5 << 8) | 128, (54 << 8) | 128, 0, lyTorch);
            fExpectInt("map reveal: level 6, 5/54 north, the view ends in the own row", lOSweep.Depth, 0);
            fExpectBool("map reveal: level 6, 5/54 north, the own tile is drawn", lOSweep.IsDrawn(0, 0), true);
            fExpectBool("map reveal: level 6, 5/54 north, the diagonal 4/54 beside is drawn", lOSweep.IsDrawn(0, -1), true);
            fExpectBool("map reveal: level 6, 5/54 north, the rock 6/54 is not", lOSweep.IsDrawn(0, 1), false);

            // The walled chamber of level 1 (29..34 / 29..34): a solid ring with diagonal corners
            // open to the corridor, the chamber's own corners diagonals open to the inside. Seen
            // from the corridor corner 28/35 looking south in the brightest light (right is west,
            // so east is a negative column), nothing of the chamber may show (per user, 2026-09-26).
            lOSweep.Run(lOLevel, (28 << 8) | 128, (35 << 8) | 128, 0x8000, lyBright);
            fExpectBool("map reveal: level 1 chamber, the corridor ahead is drawn", lOSweep.IsDrawn(4, 0), true);
            fExpectBool("map reveal: level 1 chamber, the ring's diagonal corner is drawn", lOSweep.IsDrawn(1, -1), true);
            fExpectBool("map reveal: level 1 chamber, not the chamber's diagonal 30/33", lOSweep.IsDrawn(2, -2), false);
            fExpectBool("map reveal: level 1 chamber, not the chamber 31/32", lOSweep.IsDrawn(3, -3), false);
            fExpectBool("map reveal: level 1 chamber, not the chamber 30/31", lOSweep.IsDrawn(4, -2), false);

            // The dark run of 2026-09-23 (per user, original and ours from the start of level 1):
            // straight north from 32/2 to 32/10, the original discovered from y 6 on only x 32.
            // Replayed in steps of 4 fine units on an empty map, the sweep matches all 4096 bytes
            // of the original's save; here the corridor part of it.
            lOLevel.EnsureAutomap();

            byte[] lyKept = (byte[])lOLevel.AutomapTiles.Clone();

            Array.Clear(lOLevel.AutomapTiles, 0, lOLevel.AutomapTiles.Length);

            for (int liY = (2 << 8) | 128; liY <= ((10 << 8) | 191); liY += 4)
            {
                lOSweep.Run(lOLevel, (32 << 8) | 128, liY, 0, lyDark);
                UWExplorationRules.EvaluateSweep(lOLevel, lOSweep, (piX, piY) => 0);
            }

            string lsDiscovered = string.Empty;

            for (int liY = 6; liY <= 10; liY++)
            {
                for (int liX = 28; liX <= 36; liX++)
                {
                    byte lyByte = lOLevel.AutomapTiles[(liY * 64) + liX];

                    if (lyByte != 0 && (lyByte & 0xF) < 0xA)
                        lsDiscovered += liX + "/" + liY + " ";
                }
            }

            Array.Copy(lyKept, lOLevel.AutomapTiles, lyKept.Length);
            const string DarkRunExpected = "32/6 32/7 32/8 32/9 32/10";

            if (lsDiscovered.Trim() == DarkRunExpected)
                fPass("map reveal: the dark run discovers only the corridor walked", DarkRunExpected);
            else
                fFail("map reveal: the dark run discovers only the corridor walked",
                    "got " + lsDiscovered.Trim() + ", expected " + DarkRunExpected);
        }

        /// <summary>
        /// THE MAP IS PAINTED AS UW.EXE PAINTS IT (UWAutomapPainter, ovr092_1E5 and helpers):
        /// on palette indices, each pixel the parchment plus a step. With every random value at
        /// 16383: floor +3, a wall border or a diagonal's wall pixel +7, a border towards
        /// undiscovered open floor +2, a corner +4 per step. A tile's cell starts at (3x + 7, 3y + 4) from
        /// the bottom of the 320x200 screen.
        /// </summary>
        private static void fCheckMapPainter()
        {
            Console.WriteLine();
            Console.WriteLine("Map painting");

            // One open tile at 10/10, undiscovered open floor north of it, nothing elsewhere.
            byte[] lyAutomap = new byte[64 * 64];
            lyAutomap[(10 * 64) + 10] = 0x01;
            lyAutomap[(11 * 64) + 10] = 0x0B;

            byte[] lyScreen = fPaint(lyAutomap);

            fExpectInt("map paint: floor is parchment +2..4", fScreenAt(lyScreen, 38, 35), 3);
            fExpectInt("map paint: border towards undiscovered floor +0..3", fScreenAt(lyScreen, 38, 37), 2);
            fExpectInt("map paint: border towards a wall +6..7", fScreenAt(lyScreen, 40, 35), 7);
            // A lone tile: north+east AND east+south both step the north-east corner, so twice.
            fExpectInt("map paint: north-east corner stepped twice", fScreenAt(lyScreen, 40, 37), 8);
            fExpectInt("map paint: south-west corner stepped twice", fScreenAt(lyScreen, 36, 33), 8);
            fExpectInt("map paint: no south-east corner (the original's offset)", fScreenAt(lyScreen, 40, 33), 0);

            // Two diagonals side by side: open SE at 10/10, open NW at 11/10. No border between
            // them, and both solid halves stay parchment - no knob (per user, 2026-09-26).
            lyAutomap = new byte[64 * 64];
            lyAutomap[(10 * 64) + 10] = 0x02;
            lyAutomap[(10 * 64) + 11] = 0x05;
            lyScreen = fPaint(lyAutomap);

            fExpectInt("map paint: SE diagonal, wall pixel on the diagonal", fScreenAt(lyScreen, 37, 34), 7);
            fExpectInt("map paint: SE diagonal, its solid corner untouched", fScreenAt(lyScreen, 37, 36), 0);
            fExpectInt("map paint: NW diagonal, its solid corner untouched", fScreenAt(lyScreen, 42, 34), 0);
            fExpectInt("map paint: SE diagonal, no west border", fScreenAt(lyScreen, 36, 35), 0);
            fExpectInt("map paint: SE diagonal, south border", fScreenAt(lyScreen, 38, 33), 7);
        }

        /// <summary>
        /// A CREATURE MADE IN CODE (per user, 2026-09-27: a summoned monster turned hostile): the
        /// record must not read zeros where the object has properties, and a new creature starts
        /// as ovr101_5E sets it up.
        /// </summary>
        private static void fCheckNewCreature()
        {
            Console.WriteLine();
            Console.WriteLine("New creatures");

            UWNpc lONpc = new UWNpc(70) { NPCAttitude = 3, NPCIsAlly = true, NPCGoal = 4 };
            UWCritterRecord lORecord = new UWCritterRecord(lONpc);

            fExpectInt("creature: without raw bytes the attitude comes from the object", lORecord.Attitude, 3);
            fExpectBool("creature: ... and the ally bit", lORecord.IsAlly, true);
            fExpectInt("creature: ... and the goal", lORecord.Goal, 4);

            lORecord.InitialiseAsNew(40, 0);

            fExpectInt("creature: new, attitude 2", lORecord.Attitude, 2);
            fExpectInt("creature: new, goal 8", lORecord.Goal, 8);
            fExpectBool("creature: new, no ally bit of its own", lORecord.IsAlly, false);
            fExpectInt("creature: new, hit points vitality * 0x10 / 0x20 at the lowest roll", lORecord.HitPoints, 20);
            fExpectInt("creature: new, home post x 0x20", lORecord.HomeX, 0x20);
            fExpectInt("creature: new, home post y 0x20", lORecord.HomeY, 0x20);
        }

        /// <summary>
        /// DAMAGE TO OBJECTS (DamageObjectAndDoors, DamageObject_Debris, DetonateProjectile;
        /// read 2026-09-27): the blast dice of fireball and lightning bolt, the wear by quality
        /// class and bit 13, and the remains - fire takes a pile of debris away, smoke one time
        /// in four.
        /// </summary>
        private static void fCheckObjectDamage(string psDataPath)
        {
            Console.WriteLine();
            Console.WriteLine("Damage to objects");

            fExpectBool("blast: the fireball explodes",
                UWObjectDamageRules.TryGetBlast(0x14, out int liCount, out int liRange, out int liType), true);
            fExpectInt("blast: fireball 10 dice", liCount, 10);
            fExpectInt("blast: ... of 6", liRange, 6);
            fExpectInt("blast: ... fire 0x0B", liType, 0x0B);
            UWObjectDamageRules.TryGetBlast(0x15, out liCount, out liRange, out liType);
            fExpectInt("blast: lightning bolt 6 dice", liCount, 6);
            fExpectInt("blast: ... of 5", liRange, 5);
            fExpectInt("blast: ... magic 3", liType, 3);
            fExpectBool("blast: the magic missile does not explode",
                UWObjectDamageRules.TryGetBlast(0x17, out _, out _, out _), false);

            fExpectBool("wear: class 0 loses it all", UWObjectDamageRules.Wear(40, 35, 0, false, out int liQuality), false);
            fExpectInt("wear: 40 - 35", liQuality, 5);
            UWObjectDamageRules.Wear(40, 35, 2, false, out liQuality);
            fExpectInt("wear: class 2 a quarter (35 >> 2 = 8)", liQuality, 32);
            fExpectBool("wear: class 3 immune", UWObjectDamageRules.Wear(40, 60, 3, false, out liQuality), false);
            fExpectInt("wear: class 3 unchanged", liQuality, 40);
            fExpectBool("wear: bit 13 protects", UWObjectDamageRules.Wear(1, 60, 0, true, out liQuality), false);
            fExpectBool("wear: at 0 destroyed", UWObjectDamageRules.Wear(20, 20, 0, false, out liQuality), true);
            fExpectInt("wear: never below 0", liQuality, 0);
            fExpectBool("wear: class 1 halves 1 to nothing", UWObjectDamageRules.Wear(1, 1, 1, false, out _), false);

            UWRandom.Use(new FixedRandomSource(false));
            fExpectBool("remains: fire and a zero roll raise smoke",
                UWObjectDamageRules.DecideRemains(0x05, UWDamageTypes.Fire) == UWObjectDamageRules.Remains.DebrisWithSmoke, true);
            fExpectBool("remains: fire takes a pile of debris away",
                UWObjectDamageRules.DecideRemains(0xD6, UWDamageTypes.Fire) == UWObjectDamageRules.Remains.Gone, true);
            fExpectBool("remains: lightning leaves plain debris",
                UWObjectDamageRules.DecideRemains(0x05, UWDamageTypes.Magic) == UWObjectDamageRules.Remains.Debris, true);
            fExpectInt("remains: lowest roll 0xD5", UWObjectDamageRules.RollDebrisObjectId(), 0xD5);
            fExpectInt("smoke: at least 6 units, 1.5 s (in tenths)",
                (int)Math.Round(UWObjectDamageRules.RollSmokeSeconds() * 10f), 15);
            UWRandom.Use(new FixedRandomSource(true));
            fExpectBool("remains: fire and 0x7FFF (& 3 = 3) no smoke",
                UWObjectDamageRules.DecideRemains(0x05, UWDamageTypes.Fire) == UWObjectDamageRules.Remains.Debris, true);
            fExpectInt("remains: highest roll 0xD6", UWObjectDamageRules.RollDebrisObjectId(), 0xD6);
            fExpectInt("smoke: at most 60 units, 15 s (in tenths)",
                (int)Math.Round(UWObjectDamageRules.RollSmokeSeconds() * 10f), 150);
            UWRandom.Use(null);

            fExpectBool("remains: a bag spills", UWObjectDamageRules.SpillsWhenDestroyed(0x80), true);
            fExpectBool("remains: a sword does not", UWObjectDamageRules.SpillsWhenDestroyed(0x05), false);

            UWHealth lODoor = new UWHealth();
            lODoor.Initialise(40, 0, 0, 0, 0, -1, 0);
            lODoor.IsProtected = true;
            fExpectBool("door: word 0 bit 13 takes no blow", lODoor.CanTakeDamage, false);
            lODoor.IsProtected = false;
            fExpectBool("door: without the bit a class 0 door takes one", lODoor.CanTakeDamage, true);

            fExpectInt("hallucination: the normal 64 mask reads row 5, column 7 as texel 5*64+7",
                UWHallucinationState.ScrambledTexel((5 * 64) + 13, 7, UWHallucinationState.NormalMask64, 64), (5 * 64) + 7);
            fExpectInt("hallucination: the normal 32 mask drops the row's fraction",
                UWHallucinationState.ScrambledTexel((3 * 32) + 31, 4, UWHallucinationState.NormalMask32, 32), (3 * 32) + 4);
            fExpectInt("hallucination: a random mask lets the fraction into the column",
                UWHallucinationState.ScrambledTexel((3 * 32) + 5, 4, 0x3FF, 32), (3 * 32) + 9);
            UWHallucinationState lOHallucination = new UWHallucinationState();
            fExpectInt("hallucination: masks are the normal ones while off", lOHallucination.ScrambleMasks[4], 0xFC0);
            byte[] lyLightTable = UWHallucinationState.BuildLightTableEffect();
            int liOther = UWHallucinationState.OtherPart * 16 * 256;
            int liFloor = UWHallucinationState.FloorPart * 16 * 256;
            int liWall = UWHallucinationState.WallPart * 16 * 256;

            // Counts the non-black entries of one part over a range of levels.
            Func<int, int, int, int> fLit = (piPart, piFirst, piLast) =>
            {
                int liLit = 0;

                for (int liLevel = piFirst; liLevel <= piLast; liLevel++)
                    for (int liIndex = 0; liIndex < 256; liIndex++)
                        liLit += lyLightTable[piPart + (liLevel * 256) + liIndex] != 0 ? 1 : 0;

                return liLit;
            };

            fExpectBool("hallucination: a sparse green scatter on the floor right in front", fLit(liFloor, 3, 3) > 10 && fLit(liFloor, 3, 3) < 40, true);
            fExpectInt("hallucination: walls black right in front", fLit(liWall, 0, 3), 0);
            fExpectInt("hallucination: 4 to 8 all black", fLit(liOther, 4, 8) + fLit(liFloor, 4, 8) + fLit(liWall, 4, 8), 0);
            fExpectInt("hallucination: 9 to 11 floors black", fLit(liFloor, 9, 11), 0);
            fExpectInt("hallucination: 9 to 11 walls grey", fLit(liWall, 10, 10), 128);
            fExpectInt("hallucination: 12 and 13 walls black", fLit(liWall, 12, 13), 0);
            fExpectInt("hallucination: 12 and 13 floors grey", fLit(liFloor, 12, 12), 128);
            fExpectBool("hallucination: 14 a sparse purple scatter on walls", fLit(liWall, 14, 14) > 25 && fLit(liWall, 14, 14) < 60, true);
            fExpectInt("hallucination: 14 floors and the rest black, 15 all black",
                fLit(liFloor, 14, 15) + fLit(liOther, 9, 15) + fLit(liWall, 15, 15), 0);

            string lsExe = DataPath.FindExe(psDataPath);

            if (lsExe != null)
            {
                // The grey band is the file's row 2 of segment 51 (GOG UW.EXE), entries 0 and 1 aside.
                byte[] lyExe = File.ReadAllBytes(lsExe);
                int liDiffer = 0;

                for (int liIndex = 2; liIndex < 256; liIndex++)
                {
                    int liFile = lyExe.Length > 0x4A430 + 0x300 ? lyExe[0x4A430 + 0x200 + liIndex] : -1;
                    int liOurs = lyLightTable[liFloor + (12 * 256) + liIndex];

                    liDiffer += (liFile == 255 ? 255 : 0) != liOurs ? 1 : 0;
                }

                fExpectInt("hallucination: the grey on floors is the file's row 2", liDiffer, 0);
            }
        }

        /// <summary>
        /// SUMMON MONSTER'S CHOICE (Class8Spells_seg038_E45, read 2026-09-27): id = 0x40 + level +
        /// RNG % level - the upper half -, never a passive kind, a swimmer, an empty row or 0x7B/0x7C.
        /// </summary>
        private static void fCheckSummonRoll(DataImport pOData)
        {
            Console.WriteLine();
            Console.WriteLine("Summon Monster");

            if (pOData.ObjectClassProperties == null)
            {
                fFail("summon", "no creature table");
                return;
            }

            bool lbInRange = true;
            bool lbFits = true;

            for (int liAt = 0; liAt < 400; liAt++)
            {
                int liId = UWSummonSpellRules.RollMonster(12, pOData.ObjectClassProperties);

                if (liId < 0)
                    continue;

                lbInRange &= liId >= 0x40 + 12 && liId < 0x40 + 24;

                UWObjectClassProperties.Critter lOKind;

                lbFits &= pOData.ObjectClassProperties.TryGetCritter(liId, out lOKind)
                    && lOKind.Vitality != 0 && (lOKind.Passiveness & 0x42) == 0 && liId != 0x7B && liId != 0x7C;
            }

            fExpectBool("summon: casting 12 draws from 0x4C to 0x57", lbInRange, true);
            fExpectBool("summon: never a passive kind, a swimmer or an empty row", lbFits, true);
            fExpectBool("summon: casting 12 finds something", UWSummonSpellRules.RollMonster(12, pOData.ObjectClassProperties) >= 0, true);

            // A creature made at runtime takes a slot from the level's free list at once, below
            // 256 (UWLevel.TryPlaceInFreeMobileSlot) - level 9, which no later check reads.
            UWLevel lOLevel = pOData.Levels != null && pOData.Levels.Count > 8 ? pOData.Levels[8] : null;
            UWNpc lONew = new UWNpc(70);
            int liSlot;
            bool lbPlaced = lOLevel != null && lOLevel.TryPlaceInFreeMobileSlot(lONew, out liSlot) && liSlot >= 2 && liSlot < 256
                && lOLevel.Masterlist[liSlot] == lONew;

            fExpectBool("summon: a new creature gets a mobile slot from the free list", lbPlaced, true);
        }

        private static byte[] fPaint(byte[] pyAutomap)
        {
            byte[] lyScreen = new byte[UWAutomapPainter.ScreenWidth * UWAutomapPainter.ScreenHeight];

            UWAutomapPainter.Paint(lyScreen, pyAutomap, (piX, piY, peUse) => 16383);

            return lyScreen;
        }

        /// <summary>A pixel of the painted screen, y from the bottom as the painter counts.</summary>
        private static int fScreenAt(byte[] pyScreen, int piX, int piY)
        {
            return pyScreen[((UWAutomapPainter.ScreenHeight - 1 - piY) * UWAutomapPainter.ScreenWidth) + piX];
        }

        /// <summary>A random source stuck at one end: every roll the lowest value, or every roll
        /// the highest - for testing the bounds of a roll without waiting for them.</summary>
        private sealed class FixedRandomSource : IUWRandom
        {
            private readonly bool mbHighest;

            public FixedRandomSource(bool pbHighest)
            {
                mbHighest = pbHighest;
            }

            public int Next(int piMaxExclusive)
            {
                return mbHighest ? Math.Max(0, piMaxExclusive - 1) : 0;
            }

            public int Next(int piMin, int piMaxExclusive)
            {
                return mbHighest ? Math.Max(piMin, piMaxExclusive - 1) : piMin;
            }

            public double NextDouble()
            {
                return mbHighest ? 0.9999999999999999d : 0d;
            }
        }

        private static void fCheckEasyMovement()
        {
            Console.WriteLine();
            Console.WriteLine("Easy movement");

            fExpectInt("easy: an eighth of the circle is 0x2000", UWEasyMovement.EighthStep, 0x2000);
            fExpectInt("easy: a tile is 0x100 of PLAYER.DAT 0x54", UWEasyMovement.UnitsPerTile, 0x100);
            fExpectBool("easy: the step forward is half a tile",
                UWEasyMovement.ForwardDistance * 2 == UWEasyMovement.UnitsPerTile, true);
            fExpectBool("easy: the step back is a quarter",
                UWEasyMovement.BackwardDistance * 4 == UWEasyMovement.UnitsPerTile, true);

            // From a 45 degree line it simply turns by an eighth, both ways and around zero.
            fExpectInt("easy: north turns right to the next eighth",
                UWEasyMovement.Turn(0, UWEasyMovement.CommandTurnRight), 0x2000);
            fExpectInt("easy: north turns left over zero",
                UWEasyMovement.Turn(0, UWEasyMovement.CommandTurnLeft), 0xE000);
            fExpectInt("easy: and back again",
                UWEasyMovement.Turn(0xE000, UWEasyMovement.CommandTurnRight), 0);

            // Off a line the first click SNAPS - forward onto the next, back onto the last.
            fExpectInt("easy: off the line the right turn snaps to the next",
                UWEasyMovement.Turn(0x2500, UWEasyMovement.CommandTurnRight), 0x4000);
            fExpectInt("easy: and the left turn back to the last",
                UWEasyMovement.Turn(0x2500, UWEasyMovement.CommandTurnLeft), 0x2000);
            fExpectInt("easy: one unit short of a line still snaps up",
                UWEasyMovement.Turn(0x1FFF, UWEasyMovement.CommandTurnRight), 0x2000);
            fExpectInt("easy: and down to zero",
                UWEasyMovement.Turn(0x1FFF, UWEasyMovement.CommandTurnLeft), 0);

            // Which command carries how far.
            fExpectInt("easy: the middle arrow steps forward",
                UWEasyMovement.GetStepDistance(UWEasyMovement.CommandStepForward), 0x80);
            fExpectInt("easy: the W command goes the same distance",
                UWEasyMovement.GetStepDistance(UWEasyMovement.CommandRunForward), 0x80);
            fExpectInt("easy: backward is half of that",
                UWEasyMovement.GetStepDistance(UWEasyMovement.CommandStepBack), 0x40);
            fExpectInt("easy: a turn carries nothing",
                UWEasyMovement.GetStepDistance(UWEasyMovement.CommandTurnLeft), 0);

            // Backwards is the yaw turned by half a circle.
            fExpectInt("easy: the step back goes the other way",
                UWEasyMovement.GetStepYaw(0x2000, UWEasyMovement.CommandStepBack), 0xA000);
            fExpectInt("easy: forward keeps the yaw",
                UWEasyMovement.GetStepYaw(0x2000, UWEasyMovement.CommandStepForward), 0x2000);

            fExpectInt("easy: the flag of the tile check is Levitate and Fly",
                UWEasyMovement.FloatingAbilities,
                UWPlayerVitals.LevitateBit | UWPlayerVitals.FlyBit);

            // What the flag does: step off a ledge. The user found it by trying in the
            // original, 2026-09-21 - Shift W falls, Shift S does not.
            fExpectBool("easy: the W command may step off a ledge",
                UWEasyMovement.AllowsDrop(UWEasyMovement.CommandRunForward), true);
            fExpectBool("easy: the careful step may not",
                UWEasyMovement.AllowsDrop(UWEasyMovement.CommandStepForward), false);
            fExpectBool("easy: going backwards may not either",
                UWEasyMovement.AllowsDrop(UWEasyMovement.CommandStepBack), false);
            fExpectInt("easy: without it the ground may be one floor step lower",
                UWEasyMovement.DropAllowanceZPos, 8);

            fExpectInt("easy: one step is as loud as the base plus four",
                UWCritterRules.PlayerNoiseEasyMove, 4);

            // Holding repeats. The pace is ours, not the original's - a quarter of a second
            // after the user tried it out (2026-09-21).
            fExpectBool("easy: the held key repeats every 250 ms by default",
                Math.Abs(UWEasyMovement.DefaultRepeatSeconds - 0.25f) < 0.0001f, true);
            fExpectBool("easy: and the default sits on the arrows' grid",
                Math.Abs(Math.Round(UWEasyMovement.DefaultRepeatSeconds / UWEasyMovement.RepeatStepSeconds)
                    * UWEasyMovement.RepeatStepSeconds - UWEasyMovement.DefaultRepeatSeconds) < 0.0001f, true);
            fExpectBool("easy: and the slider stays inside its ends",
                UWEasyMovement.MinRepeatSeconds < UWEasyMovement.DefaultRepeatSeconds
                    && UWEasyMovement.DefaultRepeatSeconds < UWEasyMovement.MaxRepeatSeconds, true);
        }

        private static void fCheckStealth(DataImport pOData)
        {
            Console.WriteLine();
            Console.WriteLine("Stealth");

            // The bits themselves, the way the class-3 branch builds them.
            List<UWActiveSpellEffect> lOSpells = new List<UWActiveSpellEffect>();
            UWArmourProtection.Status lOStatus = new UWArmourProtection.Status();

            lOSpells.Add(new UWActiveSpellEffect { MajorClass = 3, MinorClass = 2, Stability = 1 });
            UWArmourProtection.Compute(null, pOData, lOSpells, lOStatus);
            fExpectInt("stealth: the Stealth spell sets bit 1", lOStatus.StealthBonus,
                UWArmourProtection.StealthNoiseBit);

            lOSpells.Clear();
            lOSpells.Add(new UWActiveSpellEffect { MajorClass = 3, MinorClass = 3, Stability = 1 });
            UWArmourProtection.Compute(null, pOData, lOSpells, lOStatus);
            fExpectInt("stealth: Conceal sets bit 2", lOStatus.StealthBonus,
                UWArmourProtection.ConcealVisibilityBit);

            lOSpells.Clear();
            lOSpells.Add(new UWActiveSpellEffect { MajorClass = 3, MinorClass = 4, Stability = 1 });
            UWArmourProtection.Compute(null, pOData, lOSpells, lOStatus);
            fExpectInt("stealth: Invisibility sets bit 3", lOStatus.StealthBonus,
                UWArmourProtection.InvisibilityBit);

            // Minor class 1 is Curse, not a stealth bonus - the loop skips bit 0.
            lOSpells.Clear();
            lOSpells.Add(new UWActiveSpellEffect { MajorClass = 3, MinorClass = 1, Stability = 1 });
            UWArmourProtection.Compute(null, pOData, lOSpells, lOStatus);
            fExpectInt("stealth: minor class 1 is no stealth bonus", lOStatus.StealthBonus, 0);

            // The noise half: bit 1 takes 0x10 off, everything else leaves the base alone.
            fExpectInt("stealth: without a spell the noise base stands",
                UWArmourProtection.ApplyToNoise(13, 0), 13);
            fExpectInt("stealth: Stealth takes the noise base to zero",
                UWArmourProtection.ApplyToNoise(13, UWArmourProtection.StealthNoiseBit), 0);
            fExpectInt("stealth: Conceal does not touch the noise",
                UWArmourProtection.ApplyToNoise(13, UWArmourProtection.ConcealVisibilityBit), 13);

            // The visibility half: Conceal five, Invisibility everything, together still zero.
            fExpectInt("stealth: without a spell the visibility base stands",
                UWArmourProtection.ApplyToVisibility(15, 0), 15);
            fExpectInt("stealth: Conceal takes five off",
                UWArmourProtection.ApplyToVisibility(15, UWArmourProtection.ConcealVisibilityBit), 10);
            fExpectInt("stealth: Conceal never goes below zero",
                UWArmourProtection.ApplyToVisibility(3, UWArmourProtection.ConcealVisibilityBit), 0);
            fExpectInt("stealth: Invisibility takes all of it",
                UWArmourProtection.ApplyToVisibility(15, UWArmourProtection.InvisibilityBit), 0);
            fExpectInt("stealth: both together are still zero",
                UWArmourProtection.ApplyToVisibility(15,
                    UWArmourProtection.ConcealVisibilityBit | UWArmourProtection.InvisibilityBit), 0);

            // The movement summand, an integer division of ten over the forward speed.
            fExpectInt("stealth: standing adds nothing", UWPlayerVitals.GetMovingNoise(0f), 0);
            fExpectInt("stealth: half speed adds five", UWPlayerVitals.GetMovingNoise(0.5f), 5);
            fExpectInt("stealth: a full run adds ten", UWPlayerVitals.GetMovingNoise(1f), 10);
            fExpectInt("stealth: a creeping step adds nothing yet", UWPlayerVitals.GetMovingNoise(0.09f), 0);

            // Which is why Stealth does not silence a running player: base 0, plus ten, minus five.
            fExpectInt("stealth: a runner under Stealth is still heard at five",
                UWArmourProtection.ApplyToNoise(13, UWArmourProtection.StealthNoiseBit)
                    + UWPlayerVitals.GetMovingNoise(1f) - UWCritterRules.PlayerNoiseMovingOffset, 5);
            fExpectInt("stealth: at half speed he is silent",
                Math.Max(0, UWArmourProtection.ApplyToNoise(13, UWArmourProtection.StealthNoiseBit)
                    + UWPlayerVitals.GetMovingNoise(0.5f) - UWCritterRules.PlayerNoiseMovingOffset), 0);
        }

        private static void fCheckPlayerStatus(DataImport pOData)
        {
            Console.WriteLine();
            Console.WriteLine("Player status pass");

            FakeEquipment lOWorn = new FakeEquipment();

            // A full set at full quality. From the tables: chest 0x22 and helmet 0x2E carry 6,
            // gloves 0x28 and boots 0x2B carry 5, leggings 0x25 carry 6, the shield 0x3B 3.
            lOWorn.Put(UWArmorItemMap.BodySlot.Chest, 0x22, UWPlayerCritterRow.MaxQuality);
            lOWorn.Put(UWArmorItemMap.BodySlot.Helmet, 0x2E, UWPlayerCritterRow.MaxQuality);
            lOWorn.Put(UWArmorItemMap.BodySlot.Gloves, 0x28, UWPlayerCritterRow.MaxQuality);
            lOWorn.Put(UWArmorItemMap.BodySlot.Legs, 0x25, UWPlayerCritterRow.MaxQuality);
            lOWorn.Put(UWArmorItemMap.BodySlot.Boots, 0x2B, UWPlayerCritterRow.MaxQuality);

            // Right-handed: the weapon in the right hand, the shield in the left (labels 7EB,
            // 887). A long sword (0x03) carries skill type 3, so the Sword skill.
            lOWorn.Put(UWArmorItemMap.BodySlot.RightHandSlot, 0x03, UWPlayerCritterRow.MaxQuality);
            lOWorn.Put(UWArmorItemMap.BodySlot.LeftHandSlot, 0x3B, UWPlayerCritterRow.MaxQuality);

            UWArmourProtection.Status lOStatus = new UWArmourProtection.Status();

            UWArmourProtection.Compute(lOWorn, pOData, null, lOStatus);

            fExpectInt("worn: body is chest 6 plus shield 3", lOStatus.Armour[UWArmourProtection.PartBody], 9);
            fExpectInt("worn: hands are gloves 5 plus shield 3", lOStatus.Armour[UWArmourProtection.PartHands], 8);
            fExpectInt("worn: legs are leggings 6 plus boots 5", lOStatus.Armour[UWArmourProtection.PartLegs], 11);
            fExpectInt("worn: the head is the helmet alone", lOStatus.Armour[UWArmourProtection.PartHead], 6);
            fExpectInt("worn: the sword decides the defence skill", (int)lOStatus.WeaponSkill, (int)UWPlayerData.Skill.Sword);
            fExpectInt("worn: defence 30 with sword 25 is 42",
                UWPlayerCritterRow.GetDefence(30, 25), 42);

            // Half quality halves the pieces (4, 4, 3, 3, 3 and the shield 2).
            lOWorn.Put(UWArmorItemMap.BodySlot.Chest, 0x22, 32);
            lOWorn.Put(UWArmorItemMap.BodySlot.LeftHandSlot, 0x3B, 32);

            UWArmourProtection.Compute(lOWorn, pOData, null, lOStatus);

            fExpectInt("worn: a half-worn chest and shield give 4 + 2", lOStatus.Armour[UWArmourProtection.PartBody], 6);

            // A bow in the weapon hand keeps the Unarmed skill (the ranged branch never sets
            // var_6), and it is no shield either.
            lOWorn.Put(UWArmorItemMap.BodySlot.RightHandSlot, 0x10, UWPlayerCritterRow.MaxQuality);

            UWArmourProtection.Compute(lOWorn, pOData, null, lOStatus);

            fExpectInt("worn: a bow leaves the Unarmed skill", (int)lOStatus.WeaponSkill, (int)UWPlayerData.Skill.Unarmed);

            // The enchantments of class 0xC: leggings with minor 2 give protection 3 on the
            // legs, a ring with minor 0x0A (bit 3 set) gives armour 3 on body AND hands.
            lOWorn.Enchant(UWArmorItemMap.BodySlot.Legs, 0xC, 0x2);
            lOWorn.Enchant(UWArmorItemMap.BodySlot.LeftRing, 0x36, 0xC, 0xA);

            UWArmourProtection.Compute(lOWorn, pOData, null, lOStatus);

            fExpectInt("enchantment: leggings of minor 2 protect the legs by 3",
                lOStatus.Protection[UWArmourProtection.PartLegs], 3);
            fExpectInt("enchantment: they do not protect the head",
                lOStatus.Protection[UWArmourProtection.PartHead], 0);
            fExpectInt("enchantment: a ring with bit 3 armours the body by 3",
                lOStatus.Armour[UWArmourProtection.PartBody], 9);
            fExpectInt("enchantment: and the hands by the same 3",
                lOStatus.Armour[UWArmourProtection.PartHands], 10);

            // THE SWITCH for the original's dead resistance nibble, asked for by the user on
            // 2026-09-20 (UWArmourProtection.ResistBlowsAddsArmour, UWSettings of the same
            // name): a class-2 enchantment of minor 4 is seen either way - the value is what
            // the panel shows - but only with the switch on does it reach the armour. Off is
            // the original, where PlayerStatusUpdate writes the nibble that
            // ApplyDefenceStealthBonuses never reads.
            lOWorn.Enchant(UWArmorItemMap.BodySlot.RightRing, 0x36, 0x2, 0x4);

            UWArmourProtection.ResistBlowsAddsArmour = false;
            UWArmourProtection.Compute(lOWorn, pOData, null, lOStatus);

            int liWithoutResistance = lOStatus.Armour[UWArmourProtection.PartHead];

            fExpectInt("resistance: the value is seen", lOStatus.DamageResistance, 4);
            fExpectInt("resistance: the switch off leaves the armour alone", liWithoutResistance, 6);

            UWArmourProtection.ResistBlowsAddsArmour = true;
            UWArmourProtection.Compute(lOWorn, pOData, null, lOStatus);

            fExpectInt("resistance: the switch on reaches every part",
                lOStatus.Armour[UWArmourProtection.PartHead], liWithoutResistance + 4);

            UWArmourProtection.ResistBlowsAddsArmour = false;

            fCheckWornRegeneration(pOData, lOWorn, lOStatus);
        }

        /// <summary>
        /// The Ring of Regeneration and its mana counterpart - class 11, minor 0xE and 0xF
        /// (ActiveEnchantedItem_Spells_ovr133_347, branches HealthRegeneration_ovr133_45D and
        /// ManaRegeneration_ovr133_468). Pinned here are the two halves of the rule: the
        /// status pass collecting the bits, and the tick turning one bit into exactly one
        /// point, capped at the maximum.
        ///
        /// THE USER MEASURED IT IN THE ORIGINAL on 2026-09-20: "about one hit point every
        /// twenty seconds". The disassembly says twenty-one - the player tick fires when the
        /// accumulated whole seconds exceed 0x14 (PlayerUpdateTick_seg024_24DC_3A4, label
        /// 4D7), which is 21 s or 5376 PIT ticks.
        /// </summary>
        private static void fCheckWornRegeneration(DataImport pOData, FakeEquipment pOWorn,
            UWArmourProtection.Status pOStatus)
        {
            // The rule in numbers.
            fExpectInt("regeneration: major class 11", UWWornRegeneration.MajorClass, 11);
            fExpectInt("regeneration: health is minor class 14", UWWornRegeneration.HealthMinorClass, 0xE);
            fExpectInt("regeneration: mana is minor class 15", UWWornRegeneration.ManaMinorClass, 0xF);
            fExpectInt("regeneration: one point per tick", UWWornRegeneration.Amount, 1);
            fExpectInt("regeneration: the interval is 21 seconds", UWWornRegeneration.IntervalSeconds, 21);
            fExpectInt("regeneration: which is 5376 PIT ticks", UWWornRegeneration.IntervalPitTicks, 5376);

            // THE SHARED TICK. Its length used to sit in four places and ran twenty seconds
            // on the Unity side (2026-09-21); everything now derives from UWPlayerTick, so
            // these checks guard that nothing drifts apart again. The Unity hosts cannot be
            // reached from here - UWCharacter.SpellTickSeconds and
            // UWSettings.SwimCheckInterval take the same constant.
            fExpectInt("tick: one player tick is 21 seconds", UWPlayerTick.Seconds, 21);

            // THE DIFFICULTY in the attack score (2026-09-21): seven points on easy, measured
            // from two saves the user made, one per setting - Standard writes 0 into PLAYER.DAT
            // byte 0xB4, Easy writes 1.
            fExpectInt("difficulty: easy gives seven points on the attack score",
                UWPlayerAttack.EasyDifficultyAttackBonus, 7);
            fExpectInt("tick: which is 5376 PIT ticks", UWPlayerTick.PitTicks, 5376);
            fExpectInt("tick: regeneration uses the shared tick",
                UWWornRegeneration.IntervalSeconds, UWPlayerTick.Seconds);
            fExpectInt("tick: three ticks are the minute", UWPlayerTick.MinuteTicks, 3);
            fExpectInt("tick: twenty-four ticks are the five minutes", UWPlayerTick.FiveMinuteTicks, 24);
            fExpectBool("tick: and the minute divides it, as the original's reset needs",
                UWPlayerTick.FiveMinuteTicks % UWPlayerTick.MinuteTicks == 0, true);
            fExpectInt("tick: so the block is eight and a half real minutes",
                UWPlayerTick.FiveMinuteTicks * UWPlayerTick.Seconds, 504);
            fExpectInt("tick: the respawn interval is the same block, 504 seconds",
                (int)UWRespawnRules.IntervalSeconds, 504);

            // The names in string block 6 are (major << 4) | minor - the proof that the two
            // branches of the disassembly are these two enchantments.
            if (pOData != null && pOData.Strings != null)
            {
                fExpectBool("regeneration: 0xBE is named Regeneration",
                    fSpellNameIs(pOData, UWWornRegeneration.MajorClass, UWWornRegeneration.HealthMinorClass, "Regeneration"), true);
                fExpectBool("regeneration: 0xBF is named Mana Regeneration",
                    fSpellNameIs(pOData, UWWornRegeneration.MajorClass, UWWornRegeneration.ManaMinorClass, "Mana Regeneration"), true);
            }

            // The status pass: a ring in either ring slot sets the bit, a helmet of another
            // class sets nothing, and taking the ring off clears it again.
            FakeEquipment lOBare = new FakeEquipment();

            lOBare.Enchant(UWArmorItemMap.BodySlot.LeftRing, 0x36,
                UWWornRegeneration.MajorClass, UWWornRegeneration.HealthMinorClass);

            UWArmourProtection.Compute(lOBare, pOData, null, pOStatus);

            fExpectInt("regeneration: a worn ring sets the health bit",
                pOStatus.RegenerationBits, UWWornRegeneration.HealthBit);

            lOBare.Enchant(UWArmorItemMap.BodySlot.RightRing, 0x36,
                UWWornRegeneration.MajorClass, UWWornRegeneration.ManaMinorClass);

            UWArmourProtection.Compute(lOBare, pOData, null, pOStatus);

            fExpectInt("regeneration: two rings set both bits",
                pOStatus.RegenerationBits, UWWornRegeneration.HealthBit | UWWornRegeneration.ManaBit);

            UWArmourProtection.Compute(pOWorn, pOData, null, pOStatus);

            fExpectInt("regeneration: without the rings the bits are gone", pOStatus.RegenerationBits, 0);

            // The tick: one point per tick and not one more, and the cap holds. Wounded at
            // 10 of 30, so there is room for the ring to work.
            FakeVitalsHost lOHost = new FakeVitalsHost { Regeneration = UWWornRegeneration.HealthBit };
            UWPlayerVitals lOVitals = fWoundedVitals(lOHost);

            lOVitals.RunGameTick(0, 0);

            fExpectInt("regeneration: one tick gives exactly one hit point", (int)lOVitals.CurrentHP, 11);

            for (int liTick = 0; liTick < 5; liTick++)
                lOVitals.RunGameTick(0, 0);

            fExpectInt("regeneration: five more ticks give five more", (int)lOVitals.CurrentHP, 16);

            // Up to the cap and no further. Twenty ticks are more than the missing fourteen.
            for (int liTick = 0; liTick < 20; liTick++)
                lOVitals.RunGameTick(0, 0);

            fExpectInt("regeneration: it stops at the maximum", (int)lOVitals.CurrentHP, 30);

            // The mana bit on its own, on the same tick. A fresh character, and only two
            // ticks, because the third is the minute tick on which the Mana skill regenerates
            // by its own rule (fRegenerateMana) and would blur the number.
            FakeVitalsHost lOManaHost = new FakeVitalsHost { Regeneration = UWWornRegeneration.ManaBit };
            UWPlayerVitals lOManaVitals = fWoundedVitals(lOManaHost);

            lOManaVitals.RunGameTick(0, 0);
            lOManaVitals.RunGameTick(0, 0);

            fExpectInt("regeneration: the mana bit gives one mana per tick", (int)lOManaVitals.CurrentMana, 7);
            fExpectInt("regeneration: and leaves the hit points alone", (int)lOManaVitals.CurrentHP, 10);

            // No ring, no point.
            FakeVitalsHost lOBareHost = new FakeVitalsHost();
            UWPlayerVitals lOBareVitals = fWoundedVitals(lOBareHost);

            lOBareVitals.RunGameTick(0, 0);
            lOBareVitals.RunGameTick(0, 0);

            fExpectInt("regeneration: without a ring the hit points stay", (int)lOBareVitals.CurrentHP, 10);
            fExpectInt("regeneration: and so does the mana", (int)lOBareVitals.CurrentMana, 5);

            // NATURAL HEALING (PlayerUpdates, five-minute block): one point at most per block,
            // on a Strength check against 15 - not the sleep formula it used to be.
            UWPlayerVitals lOHealVitals = fWoundedVitals(new FakeVitalsHost());

            for (int liTick = 0; liTick < UWPlayerTick.FiveMinuteTicks; liTick++)
                lOHealVitals.RunGameTick(0, 0);

            fExpectBool("healing: a five-minute block gives at most one hit point",
                lOHealVitals.CurrentHP >= 10 && lOHealVitals.CurrentHP <= 11, true);
            fExpectInt("healing: the meal counter counts the block", lOHealVitals.MealHealCounter, 1);

            // A MEAL HEALS an eighth of the blocks since the last one (ChangeHunger_ovr143_1107):
            // after eight blocks one point, and the count starts again.
            for (int liTick = 0; liTick < 7 * UWPlayerTick.FiveMinuteTicks; liTick++)
                lOHealVitals.RunGameTick(0, 0);

            float lfBeforeMeal = lOHealVitals.CurrentHP;

            fExpectBool("meal: eating succeeds", lOHealVitals.ChangeHunger(1), true);
            fExpectInt("meal: eight blocks heal one point", (int)(lOHealVitals.CurrentHP - lfBeforeMeal), 1);
            fExpectInt("meal: and the counter starts again", lOHealVitals.MealHealCounter, 0);
            fExpectBool("meal: beyond 255 nothing is eaten", lOHealVitals.ChangeHunger(UWPlayerVitals.FullHunger), false);
        }

        /// <summary>A character with 10 of 30 hit points and 5 of 20 mana, without a save game
        /// and without a skill preset - the latter would set the mana to its maximum.</summary>
        private static UWPlayerVitals fWoundedVitals(IUWVitalsHost pIHost)
        {
            UWPlayerVitals.StartValues lOStart = UWPlayerVitals.StartValues.Default;

            lOStart.FallbackMaxHP = 30f;
            lOStart.FallbackMaxMana = 20f;
            lOStart.FallbackCurrentHP = 10f;
            lOStart.FallbackCurrentMana = 5f;

            UWPlayerVitals lOVitals = new UWPlayerVitals(pIHost);

            lOVitals.Initialise(null, lOStart);

            return lOVitals;
        }

        /// <summary>The name an enchantment gets from string block 6 - the index is
        /// (major &lt;&lt; 4) | minor, plus the project's usual one.</summary>
        private static bool fSpellNameIs(DataImport pOData, int piMajor, int piMinor, string psExpected)
        {
            try
            {
                string lsName = pOData.Strings.Blocks[UWEnchantment.SpellStringBlock]
                    .Strings[((piMajor << 4) | piMinor) + 1];

                return lsName != null && lsName.TrimEnd('\r', '\n') == psExpected;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>The player's surroundings for a tick, without any engine. Only the
        /// regeneration bits are interesting here; everything else is quiet.</summary>
        private sealed class FakeVitalsHost : IUWVitalsHost
        {
            public int Regeneration;

            public void OnVitalsChanged() { }

            public void OnDamageRecorded() { }

            public void ShakeOnDamage(int piLevel) { }

            public void ShakeUnsteady(int piDuration) { }

            public void AddMessage(string psMessage) { }

            public void OnKillRewarded(bool pbRewarded) { }

            public void OnCurseStruck() { }

            public void ResetIdentification() { }

            public void OnActiveSpellsChanged(bool pbHastened, bool pbRoamingSight, bool pbTimeFrozen, bool pbTelekinesis) { }

            public int WornCurseDice => 0;

            public int DungeonLevelIndex { get; set; }

            public int WornRegeneration => Regeneration;

            public bool IsMoving => false;

            public float MovementFraction => 0f;

            public bool IsInLiquid => false;

            public bool IsChargingAttack => false;
        }

        /// <summary>The worn equipment for the status pass, without any engine.</summary>
        private sealed class FakeEquipment : IUWEquipment
        {
            private readonly Dictionary<UWArmorItemMap.BodySlot, UWObject> mOWorn =
                new Dictionary<UWArmorItemMap.BodySlot, UWObject>();

            public bool IsLeftHanded { get; set; }

            public UWObject GetEquipped(UWArmorItemMap.BodySlot peSlot)
            {
                UWObject lOItem;

                return mOWorn.TryGetValue(peSlot, out lOItem) ? lOItem : null;
            }

            public void Put(UWArmorItemMap.BodySlot peSlot, int piId, int piQuality)
            {
                mOWorn[peSlot] = new UWObject((ushort)piId) { Quality = (ushort)piQuality };
            }

            /// <summary>An enchantment on the piece already in the slot: major and minor class
            /// sit in the quantity field, see UWArmourProtection.TryGetWearableEnchantment.
            /// </summary>
            public void Enchant(UWArmorItemMap.BodySlot peSlot, int piMajor, int piMinor)
            {
                UWObject lOItem = GetEquipped(peSlot);

                lOItem.IsEnchanted = true;
                lOItem.Quantity = (ushort)((piMajor << 4) | piMinor);
            }

            public void Enchant(UWArmorItemMap.BodySlot peSlot, int piId, int piMajor, int piMinor)
            {
                Put(peSlot, piId, UWPlayerCritterRow.MaxQuality);
                Enchant(peSlot, piMajor, piMinor);
            }
        }

        // ------------------------------------------------- output

        private static void fPass(string psName, string psDetail)
        {
            Console.WriteLine(string.Format("  OK   {0,-10} {1}", psName, psDetail));
        }

        private static void fFail(string psName, string psDetail)
        {
            miFailures++;

            Console.WriteLine(string.Format("  FAIL {0,-10} {1}", psName, psDetail));
        }
    }
}
