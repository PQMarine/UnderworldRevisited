using UnityEngine;
using UWDataImport;
using UWDataImport.UWData;

/// <summary>
/// The road to the end of Ultima Underworld 1: bury Garamon, throw the eight talismans into
/// the volcano, get pulled into the Ethereal Void (per user, 2026-09-13: "build the three
/// steps"). Plus the gravestones with picture and inscription.
///
/// SOURCES: the burial exists only in UW.EXE (UW1_asm.asm, UseBones_seg040_719) - the
/// reference reads the two bits but never sets them. Talismans and void follow the
/// reference (motion.ObjectHitsFloorTile, etherealvoid.LaunchPlayerIntoTheVoid/MoveCameraToVoid),
/// gravestones follow gravestone.cs.
/// </summary>
public static class UWEndgame
{
    /// <summary>a_gravestone.</summary>
    public const int GravestoneObjectId = UWEndgameRules.GravestoneObjectId;

    /// <summary>Bones are objects 0xC2 to 0xC6 (UW.EXE seg040_352B_919).</summary>
    public const int FirstBonesObjectId = UWEndgameRules.FirstBonesObjectId;

    public const int LastBonesObjectId = UWEndgameRules.LastBonesObjectId;

    /// <summary>Garamon's bones carry this owner value (level 8, tile 2/15).</summary>
    public const int GaramonBonesOwner = UWEndgameRules.GaramonBonesOwner;

    /// <summary>Garamon's grave: level 5, tile 54/54, "The empty grave of Garamon".</summary>
    public const int GaramonGrave = UWEndgameRules.GaramonGrave;

    /// <summary>After the burial: "The grave of Garamon" (UW.EXE sets the link to 0x222).
    /// </summary>
    public const int GaramonBuriedGrave = UWEndgameRules.GaramonBuriedGrave;

    /// <summary>The tile whose first object fires as a trigger after the burial - on
    /// level 5 an a_move trigger (UW.EXE: GetTile(0x36, 0x34)); the rule set's
    /// story.burialTrigger (cleanup phase 10), UWQuestPlaces' consts by default.</summary>
    private static int BurialTriggerTileX => UWRules.Current.Story.BurialTrigger.TileX;

    private static int BurialTriggerTileY => UWRules.Current.Story.BurialTrigger.TileY;

    /// <summary>Conversation with Garamon's ghost.</summary>
    private const int GaramonConversation = UWEndgameRules.GaramonConversation;

    /// <summary>String block 1. UW.EXE calls 0x84, 0x86, 0x103 and 0x116 - our export is exactly
    /// one higher for all four (the usual +1 in this project, checked with the removed tool UWGaramonDump:
    /// at 0x84 we have "The door is spiked."). The three messages of the launch (0x116 to 0x118)
    /// live in UWEndgameSequence since 2026-10-17.</summary>
    private const int NoEffectMessage = UWStringIds.NoEffectMessage; // It seems to have no effect.

    private const int FinalRestingPlaceMessage = 0x86 + 1; // You thoughtfully give the bones a final resting place.

    private const int NotAtRestMessage = 0x103 + 1;        // The bones do not seem at rest in the grave, ...

    /// <summary>The flame sprite a destroyed talisman turns into (reference 0x1C2).</summary>
    private const int TalismanFlameObjectId = 0x1C2;

    /// <summary>Destination in the void: level 9 (zero-based), tile 27/23 (UWEndgameRules.VoidTileX
    /// and VoidTileY). The volcano centre where the moongate appears, the gate's link and the
    /// flight's steps live in UWEndgameSequence and UWMoongateFlight since 2026-10-17. The rule
    /// set's levels.void since cleanup phase 10 (UWLevelIds.Void by default).</summary>
    public static int VoidLevelIndex => UWRules.Current.Levels.Void;

    /// <summary>
    /// Does no spell work here? UW.EXE CastSpells_seg038_3307_78 aborts when the tile carries
    /// the no-magic bit or the level is the void (DungeonLevel 9) - for runes, wands and
    /// spell traps alike (the reference's spellcasting.CastSpell does the same).
    /// In the original neither Time Freeze nor Gate Travel works on level 9 (per user, 2026-09-14).
    /// </summary>
    public static bool IsMagicBlockedAt(UWLevelLoader pOLoader, Vector3 pOWorldPosition)
    {
        if (pOLoader == null || pOLoader.CurrentLevel == null)
            return false;

        return UWEndgameRules.IsMagicBlocked(pOLoader.CurrentLevel, pOLoader.CurrentLevelIndex,
            pOLoader.WorldPositionToTile(pOWorldPosition));
    }

    /// <summary>The three flag bits live in UWGameFlags (seeded by UWLevelLoader, saved by
    /// UWSavegameWriter); these forward for the old callers.</summary>
    public static bool TalismansDestroyable
    {
        get { return UWGameFlags.TalismansDestroyable; }
        set { UWGameFlags.TalismansDestroyable = value; }
    }

    /// <summary>Player data 0x62 bit 3: Garamon is buried.</summary>
    public static bool GaramonBuried
    {
        get { return UWGameFlags.GaramonBuried; }
        set { UWGameFlags.GaramonBuried = value; }
    }

    /// <summary>Player data 0x60 bit 7: the Cup of Wonder has appeared (see
    /// UWInstrumentPlayer).</summary>
    public static bool CupOfWonderFound
    {
        get { return UWGameFlags.CupOfWonderFound; }
        set { UWGameFlags.CupOfWonderFound = value; }
    }

    // ------------------------------------------------- Gravestones

    /// <summary>The grave number of a gravestone - it is stored as a special value (from 512) in
    /// the link field -, or minus one.</summary>
    public static int GetGraveNumber(UWObject pOObject)
    {
        return UWEndgameRules.GetGraveNumber(pOObject);
    }

    /// <summary>The picture in CS401.N01 for a grave number: GRAVE.DAT holds one byte per number,
    /// picture plus one; zero means no picture.</summary>
    public static int GetGraveFrame(DataImport pOData, int piGrave)
    {
        return UWEndgameRules.GetGraveFrame(pOData, piGrave);
    }

    /// <summary>The inscription: string block 8 at grave number plus one (grave 33 "The empty grave
    /// of Garamon", 34 "The grave of Garamon", 66 Korianous - checked with the removed tool UWGaramonDump).
    /// </summary>
    public static string GetGraveText(DataImport pOData, int piGrave)
    {
        return UWEndgameRules.GetGraveText(pOData, piGrave);
    }

    // ------------------------------------------------- Burial

    public static bool IsBones(int piObjectId)
    {
        return UWEndgameRules.IsBones(piObjectId);
    }

    /// <summary>
    /// Bones used on a target - UW.EXE UseBones_seg040_719:
    ///   no gravestone                   "It seems to have no effect."
    ///   ordinary bones at a grave       "You thoughtfully give the bones a final resting
    ///                                   place." - the bones are gone
    ///   Garamon's bones, other grave    "The bones do not seem at rest in the grave, and you
    ///                                   take them back." - you keep them
    ///   Garamon's bones, his grave      set both bits, grave to "The grave of Garamon",
    ///                                   conversation with Garamon's ghost, fire the trigger
    ///                                   at 54/52, bones gone
    /// Always returns true - the bones report themselves what happened.
    /// </summary>
    public static bool UseBones(UWObject pOBones, UWEntityInfo pOTarget, Interaction pOInteraction,
        UWInventory pOInventory, UWLevelLoader pOLoader)
    {
        UWObject lOTarget = pOTarget != null ? pOTarget.ObjectData : null;
        bool lbTalismansDestroyable = TalismansDestroyable;
        bool lbGaramonBuried = GaramonBuried;

        UWEndgameRules.BonesOutcome leOutcome = UWEndgameRules.UseBones(pOBones, lOTarget,
            ref lbTalismansDestroyable, ref lbGaramonBuried);

        TalismansDestroyable = lbTalismansDestroyable;
        GaramonBuried = lbGaramonBuried;

        switch (leOutcome)
        {
            case UWEndgameRules.BonesOutcome.NoEffect:
                pOInteraction.AddGeneralMessage(NoEffectMessage);
                return true;

            case UWEndgameRules.BonesOutcome.RestingPlace:
                pOInteraction.AddGeneralMessage(FinalRestingPlaceMessage);
                pOInventory.TryConsumeOne(pOBones);
                return true;

            case UWEndgameRules.BonesOutcome.NotAtRest:
                pOInteraction.AddGeneralMessage(NotAtRestMessage);
                return true;
        }

        // DELIBERATE DEVIATION FROM THE ORIGINAL (per user, 2026-09-13): the bones vanish
        // BEFORE the conversation. UW.EXE (UseBones_seg040_719) deletes them only afterwards, via
        // a pointer to the object in hand (ClearObject with ObjectInHand). That has two
        // bugs, both found by the user in the original: if you close the container they are
        // in during the conversation, or put them on the trade area (then they end up on
        // the floor at the end), they survive - and because the grave is already set to
        // 0x22, a second attempt only reports "The bones do not seem at rest in the
        // grave, and you take them back." Removed beforehand, nothing is left during the
        // conversation that could go wrong.
        if (pOInventory.CursorItem == pOBones)
            pOInventory.TakeCursorItem();
        else
            pOInventory.RemoveItem(pOBones);

        // The placeholder partner from UseBones (var_22 on the stack): object 0x7E, goal 7,
        // attitude 3, conversation slot 0x1B - in no tile. The conversation reads its
        // npc_ values from it and writes back into it at the end; afterwards it is forgotten.
        UWNpc lOPlaceholder = UWEndgameRules.CreateBurialPlaceholder();

        if (!pOInteraction.TryTalkToNpcData(lOPlaceholder))
            pOInteraction.BeginConversation(GaramonConversation);

        if (pOLoader != null)
            UWGameSession.Current.Schedule.AfterConversation(() => fFireBurialTrigger(pOLoader, pOInteraction));

        return true;
    }

    /// <summary>In the original TalkTo runs to the end before the trigger at 54/52 fires -
    /// for us the conversation runs alongside, so we wait: on the world's scheduler since
    /// 2026-10-17 (UWWorldScheduler.AfterConversation, which also covers a conversation that has
    /// not opened yet); until then a coroutine on the level loader that polled the conversation
    /// every frame.
    ///
    /// BUG OF THE ORIGINAL, DELIBERATELY REPRODUCED (per user, 2026-09-13): the
    /// burial conversation only has a placeholder partner on the stack (UseBones var_22,
    /// attitude 3). What it writes back at the end (RetrieveImportedVariablesAfterConversation)
    /// ends up there and not on the Garamon the trigger then creates from the template.
    /// Whoever already answers up to "I shall think on it." here has a friendly Garamon; only
    /// a second conversation with the appeared ghost makes him mellow. So for us the
    /// burial conversation runs with the same placeholder, which stands in no tile.</summary>
    private static void fFireBurialTrigger(UWLevelLoader pOLoader, Interaction pOInteraction)
    {
        UWLevel lOLevel = pOLoader != null ? pOLoader.CurrentLevel : null;

        if (lOLevel == null)
            return;

        UWTile lOTile = lOLevel.GetTile(BurialTriggerTileX, BurialTriggerTileY);

        if (lOTile != null && lOTile.ObjectsInTile != null && lOTile.ObjectsInTile.Count > 0)
            UWTriggerSystem.FireTraps(lOTile.ObjectsInTile[0], null, pOLoader, pOInteraction);
    }

    // ------------------------------------------------- Talismans in the volcano

    /// <summary>
    /// Does a talisman fall into the lava on level 8 near the centre? Following the reference's
    /// motion.ObjectHitsFloorTile:
    ///   before the burial    set the dream bit, the talisman stays where it lies
    ///   afterwards           count down, the talisman perishes in flames; the last one
    ///                        only brings the counter to 0 with the rending sound, and
    ///                        CheckVictory starts the journey (since 2026-10-18)
    /// Returns true if the talisman is destroyed - then it vanishes like a
    /// sinking item. Called at the moment of impact (UWMotionHost.TalismanBurns).
    /// </summary>
    public static bool TryDestroyTalismanInLava(UWLevelLoader pOLoader, UWObject pOObject,
        int piTileX, int piTileZ, Vector3 pOLanding)
    {
        if (pOLoader == null || pOObject == null)
            return false;

        UWCharacter lOCharacter = UWScene.Character;
        bool lbAlive = lOCharacter == null || lOCharacter.Vitals.CurrentHP > 0;
        int liRemaining;

        UWEndgameRules.TalismanOutcome leOutcome = UWEndgameRules.DropTalismanInLava(pOObject.ID,
            pOLoader.CurrentLevelIndex, piTileX, piTileZ, lbAlive, TalismansDestroyable, out liRemaining);

        switch (leOutcome)
        {
            case UWEndgameRules.TalismanOutcome.Last:
                UWWorld lOWorld = UWWorld.Of(pOLoader);

                if (lOWorld != null)
                    lOWorld.Presentation.AddGeneralMessage(UWEndgameSequence.RendingSoundMessage);

                return true;

            case UWEndgameRules.TalismanOutcome.Flame:
                fFlame(pOLoader, pOLanding, liRemaining);
                return true;

            default:
                return false;
        }
    }


    /// <summary>How long an explosion stays.</summary>
    private const float TalismanFlameSeconds = 1.5f;

    /// <summary>
    /// The talisman turns into an explosion (0x1C2), plus explosion groups - the more talismans
    /// are already destroyed, the more (UW.EXE ObjectHitsFloorTileDestroyTalismans_seg029_C6F):
    /// for si from 8 downwards, as long as the remaining count is at most si, one group. Each
    /// group lies on a random neighbouring tile (-1 to +1) and rises 4 to 11
    /// height steps above the previous one; SpawnMultipleAnimoCopies_seg044_794 places two to four
    /// explosions there (id plus 1 or 2) with some scatter.
    ///
    /// IN UW.EXE THIS DOES NO DAMAGE: DamageObjectsInTile is only called by the flame wind
    /// (FlameWind_seg038_3307_6FB), not by this path. The explosions look the same, though.
    /// </summary>
    private static void fFlame(UWLevelLoader pOLoader, Vector3 pOLanding, int piRemaining)
    {
        pOLoader.SpawnEffectAt(TalismanFlameObjectId, pOLanding, TalismanFlameSeconds);

        float lfTile = UWWorldScale.TileSize;
        float lfStep = UWWorldScale.ZPosStep;
        float lfHeight = 0f;

        for (int liCounter = 8; piRemaining <= liCounter; liCounter--)
        {
            lfHeight += (4 + UWRandom.Next(0, 8)) * lfStep;

            Vector3 lOBurst = pOLanding + new Vector3(UWRandom.Next(-1, 2) * lfTile, lfHeight, UWRandom.Next(-1, 2) * lfTile);
            int liCopies = 2 + UWRandom.Next(0, 3);

            for (int liCopy = 0; liCopy < liCopies; liCopy++)
            {
                Vector3 lOScatter = new Vector3(UWRandom.Next(-2, 3) * lfTile / 8f, UWRandom.Next(-8, 7) * lfStep,
                    UWRandom.Next(-2, 3) * lfTile / 8f);

                pOLoader.SpawnEffectAt(TalismanFlameObjectId + 1 + UWRandom.Next(0, 2), lOBurst + lOScatter, TalismanFlameSeconds);
            }
        }
    }

    /// <summary>
    /// THE VICTORY CHECK, once per running frame (UWCritterDriver, after the world's scheduler):
    /// check_victory (UnderworldExhumed game/SKILLS.C) starts the journey whenever the talisman
    /// counter is 0, not at the impact of the last talisman - so a save with the counter at 0
    /// swirls into the void right after loading, as the user saw in the original (2026-10-18).
    /// The flight sets the counter to 0xFF at its end; while it runs it does not start again.
    /// </summary>
    public static void CheckVictory(UWLevelLoader pOLoader)
    {
        if (pOLoader == null || !pOLoader.HasWorld || IsVictoryRunning)
            return;

        if (!UWEndgameRules.AllTalismansGone() || UWGameSession.Current.Endgame.IsLaunching)
            return;

        fLaunchIntoVoid(pOLoader);
    }

    /// <summary>
    /// The talisman counter is 0: the sequence (messages, gate, the camera's 64 steps into
    /// the moongate, the journey to the void) is UWEndgameSequence on the session's scheduler since
    /// 2026-10-17 (cleanup step 3.4); until then this was a coroutine on the level loader, waiting
    /// with WaitForSeconds per step and guarded by a static flag. The wait per step is still the
    /// setting UWSettings.MoongateStepSeconds (0: one step per frame).
    /// </summary>
    private static void fLaunchIntoVoid(UWLevelLoader pOLoader)
    {
        UnderworldRevisited.UWSettings lOSettings = UnderworldRevisited.UWSettings.Instance;
        float lfStepSeconds = lOSettings != null ? lOSettings.MoongateStepSeconds : UWMoongateFlight.DefaultStepSeconds;
        UWGameSession lOSession = UWGameSession.Current;

        lOSession.Endgame.Launch(UWWorld.Of(pOLoader), new LaunchHost(), lOSession.Schedule, lfStepSeconds);
    }

    /// <summary>What the launch asks of the scene: the main camera through the remote view, with
    /// this flight's claim on it. The message scroll, the gate and the journey go to the world
    /// (UWWorld: its presentation looks the scroll up at every message, SpawnNewObject, TravelTo)
    /// since cleanup step 3.6 (2026-10-17).</summary>
    private sealed class LaunchHost : UWEndgameSequence.IHost
    {
        private UWRemoteCamera mORemote;

        private int miClaim = -1;

        public bool TryGetCamera(out UWVector3 pOPosition, out float pfYaw)
        {
            Camera lOCamera = Camera.main;

            pOPosition = new UWVector3(0f, 0f, 0f);
            pfYaw = 0f;

            if (lOCamera == null)
                return false;

            Vector3 lOAt = lOCamera.transform.position;

            pOPosition = new UWVector3(lOAt.x, lOAt.y, lOAt.z);
            pfYaw = lOCamera.transform.eulerAngles.y;

            return true;
        }

        public void BeginCamera(UWMoongateFlight.Pose pOPose)
        {
            mORemote = UWRemoteCamera.Ensure();

            if (mORemote != null)
                miClaim = mORemote.Begin(UWRemoteCamera.TrapPriority, fToVector(pOPose.Position), pOPose.Yaw, 0f, false, null);
        }

        public void SetCameraPose(UWMoongateFlight.Pose pOPose)
        {
            if (miClaim >= 0 && mORemote != null)
                mORemote.SetPose(miClaim, fToVector(pOPose.Position), pOPose.Yaw, 0f, pOPose.Roll);
        }

        public void EndCamera()
        {
            if (miClaim >= 0 && mORemote != null)
                mORemote.End(miClaim);

            miClaim = -1;
        }

        private static Vector3 fToVector(UWVector3 pOPosition)
        {
            return new Vector3(pOPosition.X, pOPosition.Y, pOPosition.Z);
        }
    }

    /// <summary>Is the ending currently running - sequence, victory screens, main menu?</summary>
    public static bool IsVictoryRunning { get; private set; }

    /// <summary>
    /// The end sequence per UW.EXE EndGameFunctions_ovr143_1186: cutscene 1, then
    /// WIN1.BYT until a key press, then WIN2.BYT with the character's stats
    /// (VictoryScreen_ovr143_76F) until a key press, then DeathEndGame(0). After that you
    /// are in the main menu (per user in the original, 2026-09-14). The reference also plays
    /// the credits first (sequence 0xA) - the original does not, so neither do we.
    ///
    /// THE WORLD STANDS STILL MEANWHILE: Time.timeScale 0 from the start. The sequence player runs
    /// on the unscaled clock, and the menu pauses the world afterwards anyway.
    /// </summary>
    public static bool PlayVictory()
    {
        if (IsVictoryRunning)
            return true;

        UWIntroPlayer lOPlayer = UWScene.IntroPlayer;

        if (lOPlayer == null)
            return false;

        IsVictoryRunning = true;
        UWGameClock.Hold(UWGameClock.VictoryHold);

        lOPlayer.PlayCutscene(UWObjectMechanics.EndgameCutscene,
            () => lOPlayer.PlayVictoryScreens(fReturnToMainMenu));

        return true;
    }

    private static void fReturnToMainMenu()
    {
        UWMainMenu lOMenu = UWScene.MainMenu;

        if (lOMenu != null)
        {
            lOMenu.ShowAfterVictory();
            return;
        }

        // Without a menu in the scene, at least the game does not continue frozen.
        UWGameClock.Release(UWGameClock.VictoryHold);
        IsVictoryRunning = false;
    }

    /// <summary>From a loaded save game or a new character: only the run state - the flags
    /// themselves are seeded into UWGameFlags by UWLevelLoader. The launch's state belongs to
    /// the session since 2026-10-17 (UWGameSession.Endgame), which every start makes new.</summary>
    public static void SeedFrom(UWPlayerData pOPlayer)
    {
        IsVictoryRunning = false;
    }
}
