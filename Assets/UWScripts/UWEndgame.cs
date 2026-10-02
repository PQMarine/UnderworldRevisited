using System.Collections;
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
    /// level 5 an a_move trigger (UW.EXE: GetTile(0x36, 0x34)).</summary>
    private const int BurialTriggerTileX = UWEndgameRules.BurialTriggerTileX;

    private const int BurialTriggerTileY = UWEndgameRules.BurialTriggerTileY;

    /// <summary>Conversation with Garamon's ghost.</summary>
    private const int GaramonConversation = UWEndgameRules.GaramonConversation;

    /// <summary>String block 1. UW.EXE calls 0x84, 0x86, 0x103 and 0x116 - our export is exactly
    /// one higher for all four (the usual +1 in this project, checked with the removed tool UWGaramonDump:
    /// at 0x84 we have "The door is spiked.").</summary>
    private const int NoEffectMessage = 0x84 + 1;          // It seems to have no effect.

    private const int FinalRestingPlaceMessage = 0x86 + 1; // You thoughtfully give the bones a final resting place.

    private const int NotAtRestMessage = 0x103 + 1;        // The bones do not seem at rest in the grave, ...

    private const int RendingSoundMessage = 0x116 + 1;     // A rending sound fills the air.

    private const int SlasherDraggedMessage = 0x117 + 1;   // The Slasher of Veils is dragged from this world.

    private const int SuckedThroughMessage = 0x118 + 1;    // You are sucked through the moongate . . . . .

    /// <summary>Quest flag 36: talismans still to destroy, 0xFF when all are gone.</summary>
    private const int TalismanQuestFlag = UWEndgameRules.TalismanQuestFlag;

    /// <summary>Quest flag 37: dreams; bit 3 is set when a talisman falls into the volcano
    /// before the burial.</summary>
    private const int DreamQuestFlag = UWEndgameRules.DreamQuestFlag;

    /// <summary>Level 8 (zero-based) and the centre of the volcano.</summary>
    private const int VolcanoLevelIndex = UWEndgameRules.VolcanoLevelIndex;

    private const int VolcanoCentre = UWEndgameRules.VolcanoCentre;

    /// <summary>Less than this many tiles of Manhattan distance to the centre.</summary>
    private const int VolcanoRadius = UWEndgameRules.VolcanoRadius;

    /// <summary>The flame sprite a destroyed talisman turns into (reference 0x1C2).</summary>
    private const int TalismanFlameObjectId = 0x1C2;

    private const int MoongateObjectId = UWObjectMechanics.MoongateObjectId;

    /// <summary>Link field of the moongate in the volcano: 0x2C0, colour 192.</summary>
    private const int MoongateLink = 0x2C0;

    /// <summary>Destination in the void: level 9 (zero-based), tile 27/23 (VoidTileX/VoidTileY).</summary>
    public const int VoidLevelIndex = UWEndgameRules.VoidLevelIndex;

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

    /// <summary>Destination tile in the void, 27/23 - see VoidLevelIndex.</summary>
    private const int VoidTileX = UWEndgameRules.VoidTileX;

    private const int VoidTileY = UWEndgameRules.VoidTileY;

    /// <summary>The camera flight to the moongate: 64 steps (reference 0.1 s each, for us
    /// UWSettings.MoongateStepSeconds). Per step 0x800 angle units of roll around the view axis
    /// (reference PositionPlayerCamera, roll = steps &lt;&lt; 0xB) and 0xCCB along the circular path
    /// around the gate, the view stays aimed at the gate - see fLaunchIntoVoid.</summary>
    private const int MoongateSteps = 0x40;

    private const float MoongateStepSeconds = 0.1f;

    private const float MoongateRollPerStep = 0x800 * 360f / 65536f;

    /// <summary>Angle along the circular path around the gate per step (reference MoveCameraToVoid: 0xCCB).
    /// </summary>
    private const float MoongateYawPerStep = 0xCCB * 360f / 65536f;

    /// <summary>Sinking per step: two fine height units (reference, steps &lt;&lt; 1), for
    /// us 0.25 world units each (UWViewpoint.UnitsPerOriginalUnit).</summary>
    private const float MoongateSinkPerStep = 2f * UWViewpoint.UnitsPerOriginalUnit;

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

    private static bool mbLaunching;

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
            pOLoader.StartCoroutine(fFireBurialTriggerAfterTalk(pOLoader, pOInteraction));

        return true;
    }

    /// <summary>In the original TalkTo runs to the end before the trigger at 54/52 fires -
    /// for us the conversation runs alongside, so we wait.
    ///
    /// BUG OF THE ORIGINAL, DELIBERATELY REPRODUCED (per user, 2026-09-13): the
    /// burial conversation only has a placeholder partner on the stack (UseBones var_22,
    /// attitude 3). What it writes back at the end (RetrieveImportedVariablesAfterConversation)
    /// ends up there and not on the Garamon the trigger then creates from the template.
    /// Whoever already answers up to "I shall think on it." here has a friendly Garamon; only
    /// a second conversation with the appeared ghost makes him mellow. So for us the
    /// burial conversation runs with the same placeholder, which stands in no tile.</summary>
    private static IEnumerator fFireBurialTriggerAfterTalk(UWLevelLoader pOLoader, Interaction pOInteraction)
    {
        yield return null;

        while (UWConversationScreen.IsAnyOpen)
            yield return null;

        UWLevel lOLevel = pOLoader.CurrentLevel;

        if (lOLevel == null)
            yield break;

        UWTile lOTile = lOLevel.TileData[(BurialTriggerTileY * 64) + BurialTriggerTileX];

        if (lOTile != null && lOTile.ObjectsInTile != null && lOTile.ObjectsInTile.Count > 0)
            UWTriggerSystem.FireTraps(lOTile.ObjectsInTile[0], null, pOLoader, pOInteraction);
    }

    // ------------------------------------------------- Talismans in the volcano

    /// <summary>
    /// Does a talisman fall into the lava on level 8 near the centre? Following the reference's
    /// motion.ObjectHitsFloorTile:
    ///   before the burial    set the dream bit, the talisman stays where it lies
    ///   afterwards           count down, the talisman perishes in flames; on the last one
    ///                        counter to 0xFF and off into the void
    /// Returns true if the talisman is destroyed - then it vanishes like a
    /// sinking item. pfDelay is the flight time until impact.
    /// </summary>
    public static bool TryDestroyTalismanInLava(UWLevelLoader pOLoader, UWObject pOObject,
        int piTileX, int piTileZ, Vector3 pOLanding, float pfDelay)
    {
        if (pOLoader == null || pOObject == null)
            return false;

        UWCharacter lOCharacter = UWScene.Character;
        bool lbAlive = lOCharacter == null || lOCharacter.CurrentHP > 0f;
        int liRemaining;

        UWEndgameRules.TalismanOutcome leOutcome = UWEndgameRules.DropTalismanInLava(pOObject.ID,
            pOLoader.CurrentLevelIndex, piTileX, piTileZ, lbAlive, TalismansDestroyable, out liRemaining);

        switch (leOutcome)
        {
            case UWEndgameRules.TalismanOutcome.Launch:
                pOLoader.StartCoroutine(fLaunchIntoVoid(pOLoader, pfDelay));
                return true;

            case UWEndgameRules.TalismanOutcome.Flame:
                pOLoader.StartCoroutine(fFlame(pOLoader, pOLanding, pfDelay, liRemaining));
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
    private static IEnumerator fFlame(UWLevelLoader pOLoader, Vector3 pOLanding, float pfDelay, int piRemaining)
    {
        if (pfDelay > 0f)
            yield return new WaitForSeconds(pfDelay);

        pOLoader.SpawnEffectAt(TalismanFlameObjectId, pOLanding, TalismanFlameSeconds);

        float lfTile = UnderworldRevisited.Build.UWLevelMeshBuilder.TileSpacing;
        float lfStep = UnderworldRevisited.Build.UWObjectSpawner.HeightScale;
        float lfHeight = 0f;

        for (int liCounter = 8; piRemaining <= liCounter; liCounter--)
        {
            lfHeight += (4 + Random.Range(0, 8)) * lfStep;

            Vector3 lOBurst = pOLanding + new Vector3(Random.Range(-1, 2) * lfTile, lfHeight, Random.Range(-1, 2) * lfTile);
            int liCopies = 2 + Random.Range(0, 3);

            for (int liCopy = 0; liCopy < liCopies; liCopy++)
            {
                Vector3 lOScatter = new Vector3(Random.Range(-2, 3) * lfTile / 8f, Random.Range(-8, 7) * lfStep,
                    Random.Range(-2, 3) * lfTile / 8f);

                pOLoader.SpawnEffectAt(TalismanFlameObjectId + 1 + Random.Range(0, 2), lOBurst + lOScatter, TalismanFlameSeconds);
            }
        }
    }

    /// <summary>
    /// The last talisman is destroyed - sequence per UW.EXE:
    ///   on impact           "A rending sound fills the air."
    ///   EndGameFunctions    moongate in the volcano centre (link 0x2C0),
    ///                       "The Slasher of Veils is dragged from this world.",
    ///                       LaunchPlayerAtMoongate_ovr134_D52: 64 steps, ONE FRAME PER STEP
    ///                       (renderrelated_seg031_351, no delay - the speed depended on the
    ///                       machine speed), then remove the gate, teleport to level 9
    ///                       27/23, "You are sucked through the moongate . . . . ."
    /// Camera per step following PositionCameraAtObject_seg034_2F89_B99, see below.
    /// </summary>
    private static IEnumerator fLaunchIntoVoid(UWLevelLoader pOLoader, float pfDelay)
    {
        if (mbLaunching)
            yield break;

        mbLaunching = true;

        if (pfDelay > 0f)
            yield return new WaitForSeconds(pfDelay);

        Interaction lOInteraction = UWScene.Interaction;

        if (lOInteraction != null)
            lOInteraction.AddGeneralMessage(RendingSoundMessage);

        // Link 0x2C0 as in UW.EXE (EndGameFunctions: word 3 = ... | 0xB000, quantity bit set) -
        // for the moongate that is the colour, see UWObjectSpawner.fGetModelFaceColor. Without it
        // the gate stayed pink (per user, 2026-09-14).
        pOLoader.SpawnObjectById(MoongateObjectId, VolcanoCentre, VolcanoCentre, MoongateLink);

        if (lOInteraction != null)
            lOInteraction.AddGeneralMessage(SlasherDraggedMessage);

        Camera lOCamera = Camera.main;
        UWRemoteCamera lORemote = UWRemoteCamera.Ensure();
        int liClaim = -1;
        Vector3 lOStart = lOCamera != null ? lOCamera.transform.position : Vector3.zero;
        Vector3 lOGate = new Vector3(VolcanoCentre * UnderworldRevisited.Build.UWLevelMeshBuilder.TileSpacing, lOStart.y,
            VolcanoCentre * UnderworldRevisited.Build.UWLevelMeshBuilder.TileSpacing);

        // THE CAMERA CIRCLES THE GATE (reference PositionPlayerCamera, MoongateSucking): the
        // angle grows by 0xCCB per step, the distance shrinks linearly from the starting distance
        // to zero, the view always points at the gate, plus the roll and a sinking by two
        // fine height units per step. Because the view is locked to the gate, it looks as if
        // the gate rotates - as in the original (per user, 2026-09-14; the gates on level 9 do
        // not rotate, so the model itself does not either). The direction of rotation is not confirmed.
        Vector3 lOFromGate = lOStart - lOGate;
        float lfRadius = lOFromGate.magnitude;
        float lfStartAngle = lfRadius > 0.0001f
            ? Mathf.Atan2(lOFromGate.x, lOFromGate.z) * Mathf.Rad2Deg
            : (lOCamera != null ? lOCamera.transform.eulerAngles.y + 180f : 0f);

        if (lORemote != null && lOCamera != null)
            liClaim = lORemote.Begin(UWRemoteCamera.TrapPriority, lOStart, lfStartAngle + 180f, 0f, false, null);

        UnderworldRevisited.UWSettings lOSettings = UnderworldRevisited.UWSettings.Instance;
        float lfStepSeconds = lOSettings != null ? lOSettings.MoongateStepSeconds : MoongateStepSeconds;

        // Like UW.EXE: draw first, then count on - step 0 is still at the starting position.
        for (int liStep = 0; liStep < MoongateSteps; liStep++)
        {
            float lfAngle = lfStartAngle + (liStep * MoongateYawPerStep);
            float lfDistance = lfRadius * (MoongateSteps - liStep) / MoongateSteps;
            Vector3 lOPosition = lOGate + (new Vector3(Mathf.Sin(lfAngle * Mathf.Deg2Rad), 0f, Mathf.Cos(lfAngle * Mathf.Deg2Rad)) * lfDistance)
                - new Vector3(0f, liStep * MoongateSinkPerStep, 0f);

            if (liClaim >= 0)
                lORemote.SetPose(liClaim, lOPosition, lfAngle + 180f, 0f, liStep * MoongateRollPerStep);

            // 0 means one step per frame, as in the original.
            if (lfStepSeconds > 0f)
                yield return new WaitForSeconds(lfStepSeconds);
            else
                yield return null;
        }

        if (liClaim >= 0)
            lORemote.End(liClaim);

        mbLaunching = false;

        pOLoader.TravelTo(VoidLevelIndex, VoidTileX, VoidTileY);

        lOInteraction = UWScene.Interaction;

        if (lOInteraction != null)
            lOInteraction.AddGeneralMessage(SuckedThroughMessage);
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
    /// themselves are seeded into UWGameFlags by UWLevelLoader.</summary>
    public static void SeedFrom(UWPlayerData pOPlayer)
    {
        mbLaunching = false;
        IsVictoryRunning = false;
    }
}
