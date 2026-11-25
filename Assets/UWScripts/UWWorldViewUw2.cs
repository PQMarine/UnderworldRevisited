using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UWDataImport;
using UWDataImport.UWData;

/// <summary>
/// THE LIGHT OF THE SECOND GAME'S LEVELS IN ITS SESSION (UW2 plan phase 2 step 3, 2026-10-29): DL.DAT
/// gives each level a minimum light, which the tile's light bit turns on or off
/// (UWDarknessUw2.TileMinimum, load_dl and change_GrSq). The tile is the
/// one under the camera; its minimum goes to UWLighting.LevelMinimum whenever the tile or the level
/// changes. The first game never has this component.
///
/// AND ITS DOORS (step 4): the session has no interface to use things with, so the use key of the modern
/// scheme or a click opens and closes the door in front - the one the view's centre (or the free
/// pointer) points at within DoorReach. A locked door stays shut; its triggers and traps are the second game's
/// rules (G3, built).
/// </summary>
public class UWWorldViewUw2 : MonoBehaviour
{
    private UWDarknessUw2 mODarkness;

    private UWLighting mOLighting;

    private UWLevelLoader mOLoader;

    private int miLastLevel = -1;

    private int miLastTile = -1;

    /// <summary>The session's view, for the modern HUD's target name.</summary>
    public static UWWorldViewUw2 Current { get; private set; }

    /// <summary>What the view's middle is on - the crosshair's thing, as the first game names it (Interaction.
    /// AimAtCrosshair through the crosshair's ray), never the free pointer's (per user 2026-11-24, UW2 plan stage U7: the
    /// name in the middle named what the free pointer was on, even with the pointer on Classic Wide's frame). The clicks
    /// still follow the pointer (fPointerRay).</summary>
    public UWEntityInfo PointedEntity()
    {
        return fCentreEntity();
    }

    /// <summary>The first thing along the view's middle within DoorReach (the camera's viewport middle: in Classic Wide
    /// the window's, which is this game's projection centre).</summary>
    private UWEntityInfo fCentreEntity()
    {
        Camera lOCamera = UWViewpoint.Current;

        if (lOCamera == null)
            return null;

        foreach (UWWorldPicker.Hit lOHit in UWWorldPicker.Measure(lOCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f)), DoorReach).Hits)
        {
            if (lOHit.Entity != null && lOHit.Entity.ObjectData != null)
                return lOHit.Entity;
        }

        return null;
    }

    /// <summary>
    /// A LOCKED DOOR UNDER THE CROSSHAIR, as the first game's (UWModernWorldInput.GetCrosshairLock, part U4e step 2): true
    /// for a closed, locked door, with the id of the key at hand that fits (UWLockUseUw2.KeyAtHand) or -1 for none - only
    /// with the automatic key (UWWorldUse.KeyUsedAutomatically, one switch for both).
    /// </summary>
    public bool CrosshairLock(out int piKeyId)
    {
        piKeyId = -1;

        if (!UWWorldUse.KeyUsedAutomatically || mOScreen == null)
            return false;

        // The door the name beside the key icon names: the view's middle's (PointedEntity).
        UWEntityInfo lOThing = fCentreEntity();

        if (lOThing == null || lOThing.ObjectData == null || UWModernWorldInput.HidesFromCrosshair(lOThing.ObjectData.ID))
            return false;

        UWDoor lODoor = lOThing.GetComponentInParent<UWDoor>();

        if (lODoor == null || !lODoor.IsClosed || !lODoor.IsLocked)
            return false;

        UWObject lOKey = UWLockUseUw2.KeyAtHand(lODoor, mOScreen.Inventory, mOLoader);

        piKeyId = lOKey != null ? lOKey.ID : -1;

        return true;
    }

    private void Start()
    {
        Current = this;
        gameObject.AddComponent<UWVitalsUw2>();
        gameObject.AddComponent<UWAttackUw2>();
        gameObject.AddComponent<UWDeathUw2>();
        gameObject.AddComponent<UWSleepUw2>();

        // The creatures' frame tick, the session's clock with them (UWCritterDriver).
        if (GetComponent<UWCritterDriver>() == null)
            gameObject.AddComponent<UWCritterDriver>();

        mOLoader = GetComponent<UWLevelLoader>();
        mOLighting = GetComponent<UWLighting>();

        // The session leaves the level loader before its sound starts: without this the effects were the first
        // game's after its menu, or none at all.
        if (mOLoader != null && mOLoader.UWDataImporter != null)
            UWAudioService.Init(mOLoader.UWDataImporter);

        string lsData = mOLoader != null && mOLoader.UWDataImporter != null ? mOLoader.UWDataImporter.DataPath : null;
        string lsFile = lsData != null ? Path.Combine(lsData, UWDarknessUw2.FileName) : null;

        if (lsFile != null && File.Exists(lsFile))
            mODarkness = new UWDarknessUw2(File.ReadAllBytes(lsFile));
        else
            Debug.LogWarning("[Session] No " + UWDarknessUw2.FileName + " - the levels have no minimum light.");

        // The game screen around the view (phase 3 step 1).
        if (mOLoader != null)
            mOScreen = UWGameScreenUw2.Attach(Camera.main, mOLoader.UWDataImporter);

        // The session's first theme, the world's first walking theme (MAINMENU.C:224 at the end of real_start; UW2 plan
        // phase 5 part A); the menu's theme gives way at once.
        UWAudioService.Uw2Walking(UWMusicSelectorUw2.PickFirst);
    }

    private UWGameScreenUw2 mOScreen;

    /// <summary>How far a door can be used from: two tiles (a tile is 64 units).</summary>
    public const float DoorReach = 128f;


    private void Update()
    {
        fCampByKey();
        fUseDoor();
        fFollowLight();
    }

    /// <summary>The panels and the prompts that have the input while they are up.</summary>
    private bool fIsInputTaken()
    {
        // The modern game menu and its layout editor hold the world as the first game's (UWControlScheme.IsWorldInputBlocked,
        // the menu's modal hold; per user at the test of 198, 2026-11-24: things were taken in the world behind the editor).
        return UWControlScheme.SharedControls == null || UWSetupMenu.IsCapturingInput || UWGameScreenUw2.IsOptionsOpen || UWMapScreenUw2.IsOpen
            || (UWControlScheme.Shared != null && UWControlScheme.Shared.IsWorldInputBlocked)
            || UWConversationScreenUw2.IsOpen || UWGameScreenUw2.IsAskingCount || UWGameScreenUw2.IsAskingYesNo || UWGameScreenUw2.IsWaitingForPage || UWControls.IsTextEntryActive
            || (UWSleepUw2.Current != null && UWSleepUw2.Current.IsNight) || UWRoamingSight.IsAnyRunning || UWVitalsUw2.PlayerParalysed
            || UWGameScreenUw2.IsCutscenePlaying;
    }

    /// <summary>player_key_sleep.</summary>
    private void fCampByKey()
    {
        // F10 alone, as the first game's hotkey rows want it: Shift+F10 is the debug panel's spell list (per user,
        // 2026-11-06: it made camp as well).
        if (!fIsInputTaken() && UWHotkeyHost.HeldMods == UWHotkeyMods.None && UWControlScheme.SharedControls[UWAct.KeyCamp].WasPressedThisFrame()
            && UWSleepUw2.Current != null)
            UWSleepUw2.Current.Camp(mOScreen != null ? mOScreen.Inventory : null);
    }

    /// <summary>
    /// THE SESSION SAVED into SAVE1 to SAVE4 under the save folder (phase 2 step 6; since phase 3 step 4 from
    /// the options' file page with the description typed there, UWGameScreenUw2): the inventory written
    /// into the session's PLAYER.DAT first, then where the player stands, how he faces, on which level - the
    /// motion's fine position and z and the camera's yaw, as the original stores PN and PlayerFacing. Null,
    /// or what failed.
    /// </summary>
    public static string Save(int piSlot, string psDescription)
    {
        UWGameSession lOSession = UWGameSession.Current;
        UWLevelLoader lOLoader = UWScene.LevelLoader;
        UWPlayerMovement lOMovement = UWScene.Player != null ? UWScene.Player.GetComponent<UWPlayerMovement>() : null;
        string lsRoot = UnderworldRevisited.UWSettings.Instance != null ? UnderworldRevisited.UWSettings.Instance.SavegameRoot : null;
        string lsSlot = UWSavegameSlots.GetWriteSlotPath(lsRoot, piSlot);

        if (lOSession == null || lOMovement == null || lOMovement.Motion == null || lsSlot == null || lOLoader == null)
            return "no session to save";

        UWWorldSync.Capture(lOLoader);

        if (UWVitalsUw2.Current != null)
            UWVitalsUw2.Current.WriteToRecord();

        // The inventory as it is now into the session's PLAYER.DAT (phase 3 step 3 part D).
        UWGameScreenUw2 lOScreen = UWGameScreenUw2.Current;
        string lsInventoryError = lOScreen != null ? lOScreen.WriteInventoryInto(lOSession.PlayerFileUw2) : null;

        if (lsInventoryError != null)
            return lsInventoryError;

        // Modern's place as the original accepts it (UWPlayerMotion.OriginalSpot): only in the file, the player stays.
        UWDataImport.UWData.UWMotionParams lOParams = lOMovement.Motion.Params;
        int liX = lOParams.X;
        int liY = lOParams.Y;
        int liZ = lOParams.Z;

        if (lOMovement.Motion.Modern && lOMovement.Motion.OriginalSpot(out liX, out liY, out liZ)
            && (liX != (lOParams.X & 0xFFFF) || liY != (lOParams.Y & 0xFFFF)))
            Debug.Log("[Savegame] Modern's place moved to the nearest one the original accepts.");

        string lsError = UWSaveCodec.SaveUw2(lOSession, lsSlot, psDescription, lOLoader.CurrentLevelIndex, liX, liY, liZ,
            lOMovement.Motion.CameraYaw, UWSoundOptions.SoundEnabled, UWSoundOptions.MusicEnabled);

        // The action bar's places and the open bags beside the save, as the first game's (UWHelpNotes.SaveTo captures them).
        if (lsError == null)
            UWHelpNotes.SaveTo(lsSlot);

        if (lsError == null)
            Debug.Log(string.Format("[Session] Saved into {0}: level {1}, tile {2}/{3}.", lsSlot, lOLoader.CurrentLevelIndex + 1, lOParams.X >> 8, lOParams.Y >> 8));

        return lsError;
    }

    /// <summary>
    /// THE VIEW'S BUTTONS (phase 3 step 1): the modern use key uses the door in front, as since step 4.
    /// On the classic screen the left button walks (UWPlayerInput, UWGameScreenUw2's window), and the
    /// right button does what the command bar has chosen, as mous_in_3d does with RightButtonThing: none
    /// or Look looks, Use uses a door, the rest are not in the session yet. A click on the frame does
    /// nothing here.
    /// </summary>
    private void fUseDoor()
    {
        UWControls lOControls = UWControlScheme.SharedControls;

        // The options panel, the map and a conversation have the input while they are up.
        if (lOControls == null || fIsInputTaken())
            return;

        UnityEngine.InputSystem.Mouse lOClickMouse = UnityEngine.InputSystem.Mouse.current;
        bool lbClassic = UWControlScheme.Shared != null && UWControlScheme.Shared.Current != UWControlScheme.SchemeEnum.Modern;
        bool lbInView = lOClickMouse != null && (mOScreen == null || mOScreen.IsInView(UWPointer.Position));
        // A missile spell waiting for its aim has the right button (mous_in_3d's GameInputMode 3; UWAttackUw2).
        bool lbRight = lbClassic && lbInView && !UWMouseButtons.IsBlocked && lOClickMouse.rightButton.wasPressedThisFrame
            && (UWAttackUw2.Current == null || !UWAttackUw2.Current.SpellTookClick);

        UWInventoryModel lOInventory = mOScreen != null ? mOScreen.Inventory : null;

        // The look waiting for the button to come up, or for a drag that takes instead (step 3 part B).
        if (mbLookPending)
        {
            fFollowLookPress(lOClickMouse, lOInventory);

            return;
        }

        if (lbRight)
        {
            UWGameScreenUw2.CommandEnum leCommand = UWGameScreenUw2.Command;

            // An object on the pointer goes into the world (mous_in_3d with GameInputMode 1: ReturnObject).
            if (lOInventory != null && lOInventory.CursorItem != null)
            {
                if (UWAttackUw2.Current != null)
                    UWAttackUw2.Current.TakeDropPress();

                fDrop(lOInventory);
            }
            // The right click that jumps during a walk by pointer does nothing else (mous_in_3d, GameInputMode 0: with
            // the left button down every command but the fight breaks off; per user 2026-11-18 ours looked). A thing
            // on the pointer still goes (GameInputMode 1 asks no button).
            else if (UWPlayerInput.ConsumesRightClick())
                return;
            else if (leCommand == UWGameScreenUw2.CommandEnum.None || leCommand == UWGameScreenUw2.CommandEnum.Look)
                fBeginLookPress();
            else if (leCommand == UWGameScreenUw2.CommandEnum.Get)
                fTake(fPickableEntity(true), lOInventory, true);
            else if (leCommand == UWGameScreenUw2.CommandEnum.Use)
                fToggleDoor(true);
            else if (leCommand == UWGameScreenUw2.CommandEnum.Talk && mOScreen != null)
                mOScreen.TalkTo(fTalkEntity());

            // The fight's click is UWAttackUw2's; the options icon is no command of the view (mous_in_3d's player_disp
            // knows use, attack, look, get and talk) - nothing here (per user 2026-11-22: no walk placeholder any more).

            return;
        }

        // The use and look keys and the free pointer on the world are the modern scheme's only (per user, 2026-11-06: E
        // took things in the classic scheme too).
        if (!lbClassic)
            fUpdateModernActions(lOControls, lOInventory);
    }

    // ------------------------------------------------- The modern scheme's way into the world, as the first game's

    /// <summary>
    /// THE MODERN SCHEME'S KEYS AND THE FREE POINTER ON THE WORLD AS THE FIRST GAME'S (UWModernWorldInput; UW2 plan, the
    /// modern interface, "the same mechanisms as UW1", per user 2026-11-23 - until then E acted on the press and had no
    /// hold, and the free pointer's click and drag on the world did nothing): Q looks (over a window at its slot), E
    /// TAPPED does the default and acts on the release, E HELD uses directly (ModernUseHoldSeconds, the ring at the
    /// crosshair), over a window E tapped uses the slot there; the free pointer's click on a thing opens its menu and its
    /// drag takes the thing onto the pointer. The rules behind them stay this game's (fModernUse, fUseAt, fTake, fLookAt).
    /// Nothing here while something hangs on the pointer.
    /// </summary>
    private void fUpdateModernActions(UWControls pOControls, UWInventoryModel pOInventory)
    {
        if (pOInventory != null && pOInventory.CursorItem != null)
        {
            mOModernUse.Cancel();
            mbWorldPress = false;

            return;
        }

        bool lbOverUi = UWModernPointer.IsFree && UWPointer.HasMouse && UWModernUi.IsOverUi(UWPointer.Position);
        UnityEngine.InputSystem.InputAction lOLook = pOControls[UWAct.ModernLook];

        // Q: over a window the slot there (the bags first, then the action bar), else the world.
        if (lOLook.WasPressedThisFrame() && !UWInput.PadPointerTakes(lOLook))
        {
            Vector2 lOAt = UWPointer.Position;

            if (!lbOverUi)
                fLook();
            else if ((UWModernBags.Instance == null || !UWModernBags.Instance.TryLookAt(lOAt)) && UWModernActionBar.Instance != null)
                UWModernActionBar.Instance.TryLookAt(lOAt);
        }

        fUpdateModernUseKey(pOControls[UWAct.ModernUse], lbOverUi);
        fUpdateModernPointerWorld(pOInventory);
    }

    /// <summary>How long E has to be held for the direct use (UWModernWorldInput's).</summary>
    private const float ModernUseHoldSeconds = 0.4f;

    /// <summary>E's tap and hold, the first game's UWHoldTap with its policy.</summary>
    private readonly UWHoldTap mOModernUse = new UWHoldTap(ModernUseHoldSeconds, UWHoldTap.UseKey);

    /// <summary>E went down with the free pointer over a window: its tap uses the slot there, its hold does nothing.</summary>
    private bool mbModernUseOnUi;

    /// <summary>How far the hold of E has come, 0 to 1, for the crosshair's ring; -1 while there is none to show.</summary>
    public float UseHoldProgress => mOModernUse.HeldSeconds >= 0.1f && !mOModernUse.HoldDone && !mbModernUseOnUi
        ? Mathf.Clamp01(mOModernUse.HeldSeconds / ModernUseHoldSeconds) : -1f;

    private void fUpdateModernUseKey(UnityEngine.InputSystem.InputAction pOKey, bool pbOverUi)
    {
        // The gamepad's pointer takes its A as the click (the PadPointer context).
        if (pOKey.WasPressedThisFrame() && UWInput.PadPointerTakes(pOKey))
        {
            mOModernUse.Cancel();

            return;
        }

        if (pOKey.WasPressedThisFrame())
        {
            // A key or a lockpick waiting for its lock (the classic page's wait) takes the use key as its click (G4 part A).
            if (!pbOverUi && UWAttackUw2.Current != null && UWAttackUw2.Current.ApplyWaitingUseOn())
            {
                mOModernUse.Cancel();

                return;
            }

            mOModernUse.Press(Time.unscaledTime);
            mbModernUseOnUi = pbOverUi;

            return;
        }

        if (!mOModernUse.IsPressed)
            return;

        // Begun on the world and the pointer went onto a window: that press is gone.
        if (!mbModernUseOnUi && pbOverUi)
        {
            mOModernUse.Cancel();

            return;
        }

        switch (mOModernUse.Update(pOKey.IsPressed(), pOKey.WasReleasedThisFrame(), Time.unscaledTime))
        {
            case UWHoldTap.ResultEnum.Hold:
                if (!mbModernUseOnUi)
                    fModernUse(true);

                break;

            case UWHoldTap.ResultEnum.Tap:
                if (!mbModernUseOnUi)
                    fModernUse(false);
                else if (pbOverUi)
                {
                    Vector2 lOAt = UWPointer.Position;

                    // The bags first, then the action bar (UWModernWorldInput.fUseAtUi).
                    if ((UWModernBags.Instance == null || !UWModernBags.Instance.TryUseAt(lOAt)) && UWModernActionBar.Instance != null)
                        UWModernActionBar.Instance.TryUseAt(lOAt);
                }

                break;
        }
    }

    /// <summary>Screen pixels the pointer moves before a press on the world becomes a drag (UWModernWorldInput's).</summary>
    private const float WorldDragPixels = 6f;

    private bool mbWorldPress;

    private Vector2 mOWorldPressAt;

    private UWEntityInfo mOWorldPressPickable;

    private UWEntityInfo mOWorldPressThing;

    private UWEntityInfo mOWorldPressBehind;

    /// <summary>What a use from the menu works: the door or thing under the press, within the use's reach.</summary>
    private UseTarget mOWorldPressUse;

    /// <summary>
    /// THE FREE POINTER ON THE WORLD (UWModernWorldInput.fUpdateModernPointerWorld): with the weapon put away, a CLICK on
    /// a thing opens its MENU (UWModernBags.OpenWorldMenu: talk or use, pick up, look); held and DRAGGED it TAKES the
    /// thing onto the pointer (the take's rules, fTake) and the bags carry it on (BeginWorldDrag). Not over a window, not
    /// in the use mode, the split box or the menu, not while a spell waits for its click or the rune page is open.
    /// </summary>
    private void fUpdateModernPointerWorld(UWInventoryModel pOInventory)
    {
        if (!UWPointer.HasMouse || !UWModernPointer.IsFree || UWGameScreenUw2.Command == UWGameScreenUw2.CommandEnum.Fight
            || UWModernBags.IsMenuOpen || UWModernBags.IsSplitting || UWModernQuestion.IsBlocking
            || (UWModernBags.Instance != null && UWModernBags.Instance.IsUsing)
            || (UWAttackUw2.Current != null && UWAttackUw2.Current.IsAnySpellWaiting) || mOScreen == null)
        {
            mbWorldPress = false;

            return;
        }

        Vector2 lOPointer = UWPointer.Position;

        if (UWMouseButtons.LeftPressed)
        {
            mbWorldPress = !UWModernUi.IsOverUi(lOPointer);

            if (mbWorldPress)
            {
                mOWorldPressAt = lOPointer;
                mOWorldPressPickable = fPickableEntity(false);
                mOWorldPressThing = fWorldThingNow(out mOWorldPressBehind);
                mOWorldPressUse = fUseTargetNow();
            }

            return;
        }

        if (!mbWorldPress)
            return;

        bool lbMoved = Vector2.Distance(lOPointer, mOWorldPressAt) >= WorldDragPixels;

        if (UWMouseButtons.LeftHeld && lbMoved)
        {
            mbWorldPress = false;

            if (mOWorldPressPickable != null && mOWorldPressPickable.CanBePickedUp && !fTalks(mOWorldPressPickable) && pOInventory != null)
            {
                fTake(mOWorldPressPickable, pOInventory, true);

                if (pOInventory.CursorItem != null && UWModernBags.Instance != null)
                    UWModernBags.Instance.BeginWorldDrag(mOWorldPressAt);
            }

            return;
        }

        if (!UWMouseButtons.LeftHeld)
        {
            mbWorldPress = false;

            if (!lbMoved && mOWorldPressThing != null && mOWorldPressThing.ObjectData != null && UWModernBags.Instance != null)
            {
                bool lbTalks = fTalks(mOWorldPressThing);

                UWModernBags.Instance.OpenWorldMenu(mOWorldPressThing, mOWorldPressBehind, lbTalks,
                    mOWorldPressThing.CanBePickedUp && !lbTalks, mOWorldPressAt);
            }
        }
    }

    /// <summary>A creature or a wisp: talked to, not used or taken.</summary>
    private static bool fTalks(UWEntityInfo pOThing)
    {
        return pOThing != null && pOThing.ObjectData != null && (pOThing.ObjectData is UWNpc || UWTalkRulesUw2.IsWisp(pOThing.ObjectData));
    }

    /// <summary>The thing a menu is for under the pointer (UWModernWorldInput.fWorldThingAt): the frontmost thing, or the
    /// pickable one behind geometry; not a wall panel (a piece of wall).</summary>
    private UWEntityInfo fWorldThingNow(out UWEntityInfo pOPickableBehind)
    {
        pOPickableBehind = null;

        Camera lOCamera = UWViewpoint.Current;

        if (lOCamera == null)
            return null;

        UWWorldPicker.Measurement lOMeasured = fMeasureAtPointer(lOCamera);
        UWEntityInfo lOFront = lOMeasured.Frontmost != null && lOMeasured.Frontmost.ObjectData != null ? lOMeasured.Frontmost : null;
        UWEntityInfo lOTarget = lOFront ?? lOMeasured.Pickable;

        if (lOTarget == null || lOTarget.ObjectData == null || UWModernWorldInput.IsWallPanel(lOTarget.ObjectData.ID))
            return null;

        pOPickableBehind = lOTarget == lOFront && lOMeasured.Pickable != lOFront ? lOMeasured.Pickable : null;

        return lOTarget;
    }

    /// <summary>The world menu's Use and Talk (IUWModernWorld.UseThing): the direct use, as E held - a creature or a wisp
    /// is talked to, anything else used where the click measured it (within the use's reach; beyond it "You cannot use
    /// that.", as the Use command says there).</summary>
    public void UseFromMenu(UWEntityInfo pOThing)
    {
        if (pOThing == null || pOThing.ObjectData == null)
            return;

        if (fTalks(pOThing))
        {
            if (mOScreen != null)
                mOScreen.TalkTo(pOThing);

            return;
        }

        UseTarget lOAt = mOWorldPressUse;
        bool lbSame = lOAt.Thing == pOThing || (lOAt.Door != null && pOThing.GetComponentInParent<UWDoor>() == lOAt.Door);

        fUseAt(lbSame ? lOAt : default(UseTarget), true);
    }

    /// <summary>The world menu's Look (IUWModernWorld.LookAt): the look at that thing, as Q at it.</summary>
    public void LookAtThing(UWEntityInfo pOThing)
    {
        if (pOThing != null)
            fLookAt(pOThing, null);
    }

    /// <summary>The world menu's Pick up (IUWModernWorld.TakeToPointer): the take's rules, onto the pointer.</summary>
    public bool TakeToPointer(UWEntityInfo pOThing)
    {
        UWInventoryModel lOInventory = mOScreen != null ? mOScreen.Inventory : null;

        if (pOThing == null || lOInventory == null || lOInventory.CursorItem != null)
            return false;

        fTake(pOThing, lOInventory, true);

        return lOInventory.CursorItem != null;
    }

    /// <summary>
    /// WHAT E WOULD DO with what the crosshair is on, for the pad's hint under it (UWModernWorldInput.GetCrosshairPadVerb):
    /// fModernUse's tap without acting - "Talk" to a creature or a wisp, "Pick up" what can be taken, "Use" a door or a
    /// thing in reach - and null where the tap does nothing; a closed secret door gives none, as the wall it looks like.
    /// </summary>
    public string CrosshairPadVerb()
    {
        if (!UWPointer.IsLookLocked)
            return null;

        if (fTalks(fFirstEntity()))
            return UWText.T("hud.crosshair.talk");

        if (fPickableEntity(false) != null)
            return UWText.T("hud.crosshair.pick_up");

        UseTarget lOAt = fUseTargetNow();
        UWObject lOData = lOAt.Door != null ? lOAt.Door.Data : lOAt.Thing != null ? lOAt.Thing.ObjectData : null;

        if (lOData == null || UWModernWorldInput.HidesFromCrosshair(lOData.ID))
            return null;

        return UWText.T("hud.crosshair.use");
    }

    /// <summary>The thing on the pointer goes into the world, from the modern bags: dropped or thrown by the point
    /// the bags hand over, as the first game's (UWModernSessionUw1.DropOrThrowPointerItem). The system mouse is not
    /// asked there: captured, it stands in the middle of the screen, and every drop was a throw from the centre (test
    /// list 183, 2026-11-23).</summary>
    public void DropHeld(UWInventoryModel pOInventory, Vector2 pOPointer)
    {
        fDropHeld(pOInventory, pOPointer);
    }

    /// <summary>The modern use key: a creature in front is talked to; TAPPED (the default) what can be taken goes into the
    /// pack, else the door or thing in front is used; HELD (pbDirect) nothing is taken, the thing in front is used - a
    /// barrel emptied where it stands, as the first game's E held. The using itself is the second game's rules.</summary>
    private void fModernUse(bool pbDirect)
    {
        UWEntityInfo lOThing = fFirstEntity();
        UWEntityInfo lOPickable = pbDirect ? null : fPickableEntity(false);

        UWInventoryModel lOInventory = mOScreen != null ? mOScreen.Inventory : null;

        if (fTalks(lOThing) && mOScreen != null)
            mOScreen.TalkTo(lOThing);
        else if (lOPickable != null && lOInventory != null)
        {
            fTake(lOPickable, lOInventory, true);
            fStowTaken(lOInventory);
        }
        else
            fToggleDoor(true);
    }

    /// <summary>
    /// WHAT THE MODERN USE KEY TOOK GOES STRAIGHT INTO THE PACK, as on the first game (UWModernWorldInput: "straight
    /// into the backpack, as there is no pointer to put it on"; UW2 plan, the modern interface's part U4b step 3,
    /// 2026-11-23 - until then it stayed on the pointer and this screen's own bag cells opened). The take itself is
    /// the rule's (fTake: reach, weight, theft, triggers); the model then finds the place, a like stack first. Only
    /// where the pack has no room does the thing stay on the pointer, with the bag windows open and the pointer free
    /// to deal with it.
    /// </summary>
    private void fStowTaken(UWInventoryModel pOInventory)
    {
        if (pOInventory.CursorItem == null)
            return;

        if (pOInventory.ReturnCursorItem(default(UWCursorOrigin), UWInventoryModel.ReturnEnum.OrAnywhere))
        {
            pOInventory.ApplyCarriedLight();

            return;
        }

        if (mOScreen != null)
            mOScreen.OpenBags();
    }

    /// <summary>
    /// THE DEFAULT MODE'S RIGHT BUTTON (player_3dlook and player_3dget with def_mode, uw2 ui/INTERACT.C; G4 part A,
    /// per user 2026-11-13): the press looks at once, as the first game's (UWClickRules); with no command the press then
    /// waits for a drag. Dragged, a thing that can be taken is taken (or "too far away" / "cannot reach"); a creature or
    /// a wisp is talked to and anything else used when the button comes up (player_3dtalk, player_3duse, both after
    /// mouse_release) - the door, the switch or the lever under the pointer at the press. Look mode does not drag.
    /// </summary>
    private bool mbLookPending;

    /// <summary>The drag went past the threshold over a thing that is not taken: the talk or the use comes with the
    /// release.</summary>
    private bool mbDragUsePending;

    private Vector2 mOLookPressAt;

    private UWEntityInfo mOLookTarget;

    private UseTarget mOPressTarget;


    private void fBeginLookPress()
    {
        mOLookPressAt = UWPointer.Position;
        mOLookTarget = fPickableEntity(false);
        mOPressTarget = fUseTargetNow();
        mbDragUsePending = false;

        // A CREATURE IS TALKED TO AS FAR AS ONE SEES (player_3dget's def_mode branch calls player_3dtalk on the object
        // drawn under the pointer, with no reach; per user 2026-11-20 on the original: the trilkhai in Killorn Keep
        // answer a right drag from afar, ours only within the use's reach). Taking and using keep their reach.
        if (mOPressTarget.Door == null && mOPressTarget.Thing == null)
        {
            UWEntityInfo lOFar = fTalkEntity();

            if (lOFar != null && (lOFar.ObjectData is UWNpc || UWTalkRulesUw2.IsWisp(lOFar.ObjectData)))
                mOPressTarget = new UseTarget { Thing = lOFar };
        }

        fLook();

        // Look mode looks and is done (mouse_release); the default waits for the drag.
        mbLookPending = UWGameScreenUw2.Command == UWGameScreenUw2.CommandEnum.None;
    }

    private void fFollowLookPress(UnityEngine.InputSystem.Mouse pOMouse, UWInventoryModel pOInventory)
    {
        if (pOMouse == null || !pOMouse.rightButton.isPressed)
        {
            mbLookPending = false;

            if (mbDragUsePending)
            {
                mbDragUsePending = false;
                fTalkOrUse(mOPressTarget);
            }

            return;
        }

        if (mbDragUsePending || !UWDragDistance.Crossed(mOLookPressAt, UWPointer.Position))
            return;

        // The frontmost door or thing that is not taken goes to the release; else what can be taken is.
        bool lbFrontNotTaken = mOPressTarget.Door != null || (mOPressTarget.Thing != null && !mOPressTarget.Thing.CanBePickedUp);

        if (lbFrontNotTaken)
        {
            mbDragUsePending = true;

            return;
        }

        mbLookPending = false;

        if (mOLookTarget != null && mOLookTarget.CanBePickedUp)
            fTake(mOLookTarget, pOInventory, true);
    }

    /// <summary>player_3dget's def_mode branch: a creature or a wisp is talked to, anything else used.</summary>
    private void fTalkOrUse(UseTarget pOTarget)
    {
        UWObject lOData = pOTarget.Door == null && pOTarget.Thing != null ? pOTarget.Thing.ObjectData : null;

        if (lOData != null && (lOData is UWNpc || UWTalkRulesUw2.IsWisp(lOData)) && mOScreen != null)
            mOScreen.TalkTo(pOTarget.Thing);
        else
            fUseAt(pOTarget, true);
    }

    /// <summary>
    /// WHAT TAKING AIMS AT: the first pickable thing along the pointer's ray over the look's whole range,
    /// stopped by a wall, a door, a creature or a solid thing (UWWorldPicker's Pickable, as the first
    /// game's Interaction takes). The reach is judged afterwards on the tile grid (fTake, UWReachRules), so
    /// a thing too far away says so, and Telekinesis takes it. Until 2026-11-11 taking used fFirstEntity,
    /// whose ray ends at DoorReach: beyond two tiles nothing was found, so Telekinesis could not take
    /// from afar (per user, test list 25.5). pbOrFrontmost: with nothing pickable the frontmost thing, so Get
    /// on a creature or a chest still says "You cannot pick that up." (player_3dget).
    /// </summary>
    private UWEntityInfo fPickableEntity(bool pbOrFrontmost)
    {
        Camera lOCamera = UWViewpoint.Current;

        if (lOCamera == null)
            return null;

        UWWorldPicker.Measurement lOMeasured = fMeasureAtPointer(lOCamera);

        return lOMeasured.Pickable != null || !pbOrFrontmost ? lOMeasured.Pickable : lOMeasured.Frontmost;
    }

    /// <summary>
    /// What the Talk command and a right drag talk to: the frontmost thing under the pointer as far as the light's band
    /// draws (TalkTo has no reach in the original; per user 2026-11-20) - the first game's aim (UWClassicWorldGesture:
    /// UWWorldPicker.Measure's Frontmost within Interaction.IsWithinSight) and this session's look (fLook) alike.
    /// </summary>
    private UWEntityInfo fTalkEntity()
    {
        Camera lOCamera = UWViewpoint.Current;

        if (lOCamera == null)
            return null;

        UWWorldPicker.Measurement lOMeasured = fMeasureAtPointer(lOCamera);
        bool lbSeen = lOMeasured.HasFrontmost && Interaction.IsInSightBand(lOCamera.transform,
            lOMeasured.FrontmostHit.point + (lOMeasured.FrontmostHit.normal * 0.5f), fViewingDistance());

        return lbSeen && lOMeasured.Frontmost != null && lOMeasured.Frontmost.ObjectData != null ? lOMeasured.Frontmost : null;
    }

    /// <summary>The nearest thing with object data the pointer points at, within DoorReach - in the order of what is drawn
    /// along the ray (UWWorldPicker: a model at its shape, a sprite at its pixels).</summary>
    private UWEntityInfo fFirstEntity()
    {
        foreach (UWWorldPicker.Hit lOHit in fMeasuredHits(DoorReach))
        {
            if (lOHit.Entity != null && lOHit.Entity.ObjectData != null)
                return lOHit.Entity;
        }

        return null;
    }

    /// <summary>
    /// THE HITS ALONG THE POINTER'S RAY AS THE LOOK AND THE TAKING SEE THEM (UWWorldPicker.Measure; per user 2026-11-19:
    /// the potion on a shelf could be looked at but not taken - the drag asked a raw ray for its use target, whose order
    /// was the colliders' boxes, so the shelf's box round the potion came first and the drag became a use of the shelf).
    /// </summary>
    private List<UWWorldPicker.Hit> fMeasuredHits(float pfRange)
    {
        Camera lOCamera = UWViewpoint.Current;

        if (lOCamera == null)
            return new List<UWWorldPicker.Hit>();

        return fMeasureAtPointer(lOCamera, pfRange).Hits;
    }

    /// <summary>
    /// TAKING A THING FROM THE WORLD (player_3dget, the same in both games but for its strings and the
    /// second game's pressure plates): something that can be taken and lies within the hand's reach
    /// (InPickRange, UWReachRules.CanReach; the terrain between is not tested yet) leaves its tile and
    /// goes onto the pointer, a container with its contents; a stack asks how many first. The theft, the
    /// moonstone and the pick-up trigger are the second game's rules (built, fTakeCount). pbSay: Get says what it cannot
    /// take.
    /// </summary>
    private void fTake(UWEntityInfo pOEntity, UWInventoryModel pOInventory, bool pbSay)
    {
        DataImport lOData = mOLoader != null ? mOLoader.UWDataImporter : null;
        Camera lOCamera = UWViewpoint.Current;

        if (pOInventory == null || pOInventory.CursorItem != null || lOData == null || lOCamera == null || mOLoader.CurrentLevel == null)
            return;

        if (pOEntity == null || pOEntity.ObjectData == null || !pOEntity.CanBePickedUp)
        {
            // look_nothing's "Nothing to get." and player_3dget's "You cannot pick that up."
            if (pbSay)
                UWGameScreenUw2.Say(UWText.Block(lOData, 1, (pOEntity == null ? UWTalkRulesUw2.NothingFirstString + NothingGet : CannotPickUpString) + 1, string.Empty));

            return;
        }

        UWObject lOItem = pOEntity.ObjectData;
        Vector3 lOFeet = UWPlayerThrow.GetFeet(lOCamera.transform);
        int liPlayerX = UWTileQueries.WorldToEighths(lOFeet.x);
        int liPlayerY = UWTileQueries.WorldToEighths(lOFeet.z);

        if (!UWReachRules.CanReach(liPlayerX, liPlayerY, UWUnits.WorldToZPos(lOFeet.y),
            UWReachRules.ToEighths(lOItem.TileX, lOItem.XPos), UWReachRules.ToEighths(lOItem.TileY, lOItem.YPos), lOItem.ZPos,
            UWReachRules.GetPickupDistanceSquared(UWVitalsUw2.Current != null && UWVitalsUw2.Current.Vitals != null && UWVitalsUw2.Current.Vitals.HasTelekinesis), false))
        {
            // "That is too far away to take." (string 0x6A of the second game's block 1).
            UWGameScreenUw2.Say(UWText.Block(lOData, 1, TooFarToTakeString + 1, string.Empty));

            return;
        }

        if (mOScreen == null || !mOScreen.AskCount(lOItem, piCount => fTakeCount(pOEntity, pOInventory, piCount)))
            fTakeCount(pOEntity, pOInventory, UWStackRules.CountAtLeastOne(lOItem));
    }

    /// <summary>Part of a stack is counted off, the stack stays where it lies; the whole leaves the world.</summary>
    private void fTakeCount(UWEntityInfo pOEntity, UWInventoryModel pOInventory, int piCount)
    {
        UWObject lOItem = pOEntity.ObjectData;

        // EncumCheck, after the count: "That is too heavy for you to pick up."
        if (!pOInventory.CanCarry(lOItem, piCount))
        {
            UWGameScreenUw2.Say(UWText.Block(mOLoader.UWDataImporter, 1, TooHeavyString + 1, string.Empty));

            return;
        }

        if (piCount < UWStackRules.CountAtLeastOne(lOItem) && pOInventory.TakeFromStack(lOItem, piCount))
            return;

        // player_3dget before release_3d (G4 rest part 3, 2026-11-16): the book 0x138 with its door bit sets quest 106 and
        // counts as a bad deed against race 0x1C; then the theft (player_grabbed): owners nearby who see it say so.
        if (lOItem.ID == GuardedBookId && lOItem.DoorDirection && UWGameSession.Current != null && UWGameSession.Current.PlayerFileUw2 != null)
        {
            UWPlayerRecordUw2.SetQuest(UWGameSession.Current.PlayerFileUw2, GuardedBookQuest, 1);
            UWTriggerSystem.PlayerDidBadUw2(GuardedBookOwner, mOLoader, UWGameScreenUw2.Say);
        }

        UWMoonstoneRules.OnTakenFromWorldSecondGame(UWGameSession.Current != null ? UWGameSession.Current.PlayerFileUw2 : null, lOItem,
            mOLoader.CurrentLevelIndex + 1);
        UWTriggerSystem.PlayerGrabbedUw2(lOItem, mOLoader, UWGameScreenUw2.Say);

        // release_3d: the thing's pick-up triggers (mode 2) before it leaves its tile (G3 part H4).
        UWTriggerSystem.CheckTrapUw2(lOItem, UWTrapRulesUw2.PickUpMode, lOItem.TileX, lOItem.TileY, mOLoader);

        if (lOItem.GetCategory() == UWObject.ObjectCategoryEnum.Containers && mOLoader.CurrentLevel.Masterlist != null)
            lOItem.EnsureContentsLoaded(mOLoader.CurrentLevel.Masterlist);

        mOLoader.RemoveObjectFromWorld(lOItem);
        Destroy(pOEntity.gameObject);

        // check_pplate with release (0xF) on the square it left, at its height (player_3dget, G3 part H4).
        UWTriggerSystem.CheckPlatesUw2(lOItem.TileX, lOItem.TileY, lOItem.ZPos, UWTrapRulesUw2.ReleaseMode, mOLoader, UWTrapRules.Setter.Thing);
        pOInventory.PickUpToCursor(lOItem);
    }

    /// <summary>The second game's "That is too far away to take." and "There is no space to drop that."
    /// in its block 1 (INTERACT.C player_3dget, MISSILE.C ReturnObject; the port's strings count one
    /// higher).</summary>
    public const int TooFarToTakeString = 0x6A;

    public const int NoSpaceToDropString = 0x10D;

    /// <summary>player_3dget's book: 0x138 with the door bit set (quests[26] bit 2, player_did_bad(0x1C)).</summary>
    private const int GuardedBookId = 0x138;

    private const int GuardedBookQuest = 106;

    private const int GuardedBookOwner = 0x1C;

    private const int TooHeavyString = 0x6C;

    private const int NothingGet = 3;

    /// <summary>look_nothing's how for the use command (0xA6 + 0: "You cannot use that.").</summary>
    private const int NothingUse = 0;

    private const int CannotPickUpString = 0x6D;

    /// <summary>
    /// PUTTING THE OBJECT ON THE POINTER DOWN (ReturnObject's drop, the same in both games): a little in
    /// front of the player where it fits (UWPlayerThrow.TryFindDropSpot), at the tail of the tile's list;
    /// else "There is no space to drop that.". With the pointer high enough in the view it is thrown first
    /// (UWPlayerThrow.TryThrowUw2), and dropped when the start is blocked; a burning light goes out on the
    /// ground, the first game's rule (UWObjectMechanics.ExtinguishInWorld), and one thrown when it lands.
    /// </summary>
    private void fDrop(UWInventoryModel pOInventory)
    {
        fDropHeld(pOInventory, UWPointer.Position);
    }

    /// <summary>punt_mouse_obj: what is on the pointer goes down.</summary>
    public void DropCursorItem()
    {
        UWInventoryModel lOInventory = UWGameScreenUw2.Current != null ? UWGameScreenUw2.Current.Inventory : null;

        if (lOInventory != null && lOInventory.CursorItem != null)
            fDropHeld(lOInventory, UWPointer.Position);
    }

    private void fDropHeld(UWInventoryModel pOInventory, Vector2 pOPointer)
    {
        DataImport lOData = mOLoader != null ? mOLoader.UWDataImporter : null;
        Camera lOCamera = UWViewpoint.Current;
        UWObject lOItem = pOInventory.CursorItem;

        if (lOData == null || lOCamera == null || lOItem == null)
            return;

        if (fTryThrow(pOInventory, lOItem, lOCamera.transform, pOPointer))
            return;

        bool lbFits = UWPlayerThrow.TryFindDropSpot(lOItem, lOCamera.transform, mOLoader, out Vector3 lOSpot);

        // Only once it is sure to fit: else it would stay on the pointer doused.
        if (lbFits)
            UWObjectMechanics.ExtinguishInWorld(lOItem, lOData.Textures);

        if (!lbFits || !mOLoader.SpawnDroppedObject(lOItem, lOSpot, true, UWWorldEdit.Arrival.PutDown,
            UWLevelLoader.HeadingFromDirection(lOCamera.transform.forward), true, true))
        {
            UWAudioService.PlayEffect(UWGameSession.Current.Game.Combat.NoRoomSound);
            UWGameScreenUw2.Say(UWText.Block(lOData, 1, NoSpaceToDropString + 1, string.Empty));

            return;
        }

        pOInventory.ReleaseCursorItem();
        pOInventory.ApplyCarriedLight();
        fNoteMoonstone(lOItem);

        // The thing put down arrives on its square (set_phys_data's enter, then the plates; G3 part H4).
        UWTriggerSystem.CheckPlatesUw2(lOItem.TileX, lOItem.TileY, lOItem.ZPos, UWTrapRulesUw2.EnterMode, mOLoader, UWTrapRules.Setter.Thing);
    }

    private bool fTryThrow(UWInventoryModel pOInventory, UWObject pOItem, Transform pOCamera, Vector2 pOPointer)
    {
        if (UWGameScreenUw2.Current == null || UWAttackUw2.Current == null)
            return false;

        UWGameScreenUw2.Current.PointerInMissileWindow(pOPointer, out int liX, out int liY);

        if (!UWPlayerThrow.TryThrowUw2(pOItem, liX, liY, pOCamera, mOLoader, UWAttackUw2.Current))
            return false;

        pOInventory.ReleaseCursorItem();
        pOInventory.ApplyCarriedLight();
        fNoteMoonstone(pOItem);

        return true;
    }

    /// <summary>DoSpecialActions' 0x17 after ReturnObject: a moonstone put into the world, or what holds one, takes a free
    /// place of player->moonstones (UWMoonstoneRules, G4 rest part 3b).</summary>
    private void fNoteMoonstone(UWObject pOItem)
    {
        UWMoonstoneRules.OnPutIntoWorldSecondGame(UWGameSession.Current != null ? UWGameSession.Current.PlayerFileUw2 : null, pOItem,
            mOLoader.CurrentLevelIndex + 1);
    }

    /// <summary>The classic screen captures the system mouse: our pointer says where it points
    /// (UWPointer.Position); the modern scheme's locked look aims through the middle.</summary>
    private static Ray fPointerRay(Camera pOCamera)
    {
        UnityEngine.InputSystem.Mouse lOMouse = UnityEngine.InputSystem.Mouse.current;

        // Free is out of the modern look (UWPointer.IsLookLocked): the system pointer stays captured, ours is the pointer.
        return UWGameScreenUw2.IsClassicActive || (!UWPointer.IsLookLocked && lOMouse != null)
            ? pOCamera.ScreenPointToRay(UWPointer.Position)
            : pOCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
    }

    /// <summary>
    /// THE POINTER'S MEASUREMENT AS pick_3d MAKES IT (UW2 beta gate, 2026-11-25; uw2 ui/INTERACT.C pick_3d and check_around):
    /// the thing drawn under the pointer, and on the classic screen, where the pointer lands on a wall or the floor, the
    /// first thing found on a square spiral round it up to nine of the original's pixels out - the original reads its pick
    /// buffer so, and a click beside a small thing on the floor takes it. The second game only (Underworld Exhumed: the
    /// first has no check_around); the modern scheme keeps the first game's crosshair and pointer (the same mechanisms).
    /// The spiral is check_around's walk: from the pointer one step down, then turning, r steps on the r-th leg, 45 places;
    /// a step is one of the original's rows or columns of the view on the screen (its window may be widened, UWClassicWide).
    /// </summary>
    private static UWWorldPicker.Measurement fMeasureAtPointer(Camera pOCamera, float pfRange = UWWorldPicker.Range)
    {
        UWWorldPicker.Measurement lOMeasured = UWWorldPicker.Measure(fPointerRay(pOCamera), pfRange);

        // Only on a wall or the floor (the pick buffer's texture colours): a thing, a door or nothing at all stays as it is.
        if (!UWGameScreenUw2.IsClassicActive || !lOMeasured.HasFrontmost || lOMeasured.Frontmost != null)
            return lOMeasured;

        Rect lOView = pOCamera.pixelRect;

        if (lOView.width <= 0f || lOView.height <= 0f)
            return lOMeasured;

        float lfColumn = lOView.width / (UWClassicLayoutUw2.Frame.ViewWindowWidth + UWClassicWide.Extra);
        float lfRow = lOView.height / UWClassicLayoutUw2.Frame.ViewWindowHeight;
        Vector2 lOAt = UWPointer.Position;
        int liX = 0;
        int liY = 0;
        int liStepX = 0;
        int liStepY = 1;

        for (int liLeg = 1; liLeg <= AroundReach; liLeg++)
        {
            for (int liStep = 0; liStep < liLeg; liStep++)
            {
                liX += liStepX;
                liY += liStepY;

                // The original's rows count down the screen, ours up.
                Vector2 lOPoint = lOAt + new Vector2(liX * lfColumn, -liY * lfRow);

                if (!lOView.Contains(lOPoint))
                    continue;

                UWWorldPicker.Measurement lOAround = UWWorldPicker.Measure(pOCamera.ScreenPointToRay(lOPoint), pfRange);

                if (lOAround.Frontmost != null && lOAround.Frontmost.ObjectData != null)
                    return lOAround;
            }

            // Turn: a step along x becomes one up the other way, one along y becomes one along x.
            if (liStepX != 0)
            {
                liStepY = -liStepX;
                liStepX = 0;
            }
            else
            {
                liStepX = liStepY;
                liStepY = 0;
            }
        }

        return lOMeasured;
    }

    /// <summary>check_around's reach: legs of 1 to 9 steps.</summary>
    private const int AroundReach = 9;

    /// <summary>What a use aims at: the frontmost door or thing under the pointer within DoorReach (pick_3d).</summary>
    private struct UseTarget
    {
        public UWDoor Door;

        public UWEntityInfo Thing;
    }

    /// <summary>The frontmost door or thing along the pointer's ray, measured now. Until G4 part A (2026-11-13) a door
    /// anywhere along the ray came first; now the frontmost wins, so a switch beside a door is the switch.</summary>
    private UseTarget fUseTargetNow()
    {
        // TELEKINESIS LIFTS THE USE'S REACH (UWReachRules.GetDistanceSquared, the original's pick distance 0 for "any"):
        // the switches high on the wall of the Scintillus pillar hall (level 43) are worked with it, and the ray stopped
        // at DoorReach (per user, 2026-11-16). Taking had it since 2026-11-11 (fPickableEntity).
        bool lbTelekinesis = UWVitalsUw2.Current != null && UWVitalsUw2.Current.Vitals != null && UWVitalsUw2.Current.Vitals.HasTelekinesis;

        foreach (UWWorldPicker.Hit lOHit in fMeasuredHits(lbTelekinesis ? UWWorldPicker.Range : DoorReach))
        {
            UWDoor lODoor = lOHit.Raw.collider.GetComponentInParent<UWDoor>();

            if (lODoor != null)
                return new UseTarget { Door = lODoor };

            if (lOHit.Entity != null && lOHit.Entity.ObjectData != null)
                return new UseTarget { Thing = lOHit.Entity };

            // Over the long reach a wall or floor of the level ends the search: nothing behind it is used.
            if (lbTelekinesis && lOHit.Raw.collider.GetComponent<UnderworldRevisited.Build.UWLevelChunk>() != null)
                break;
        }

        return default(UseTarget);
    }

    /// <summary>The Use command and the modern use: the door or thing under the pointer now.</summary>
    private void fToggleDoor(bool pbSayOther)
    {
        fUseAt(fUseTargetNow(), pbSayOther);
    }

    /// <summary>The minor class of the wall pictures (UseRect's minor 2: what is not the lever, the switch picture or the
    /// bed is looked at).</summary>
    private const int WallPictureClass = 0x160;

    /// <summary>The rattle of a locked door (UseRect's minor 0, play_effect(0x2D)).</summary>
    private const int LockedDoorSound = 0x2D;

    /// <summary>
    /// UseObj on a thing in the world (uw2 obj/OBJUSE.C, USEITEMS.C UseRect; G4 part A): a door opens or closes, a locked
    /// one says "The door is locked." with its rattle; the bed sleeps; a switch flips and the lever and the switch picture
    /// step (UWUseRulesUw2); another wall picture is looked at. Every use ends with checkTrap(who, obj, 4) - its use
    /// triggers. pbSayOther: a use not built yet says so.
    /// </summary>
    private void fUseAt(UseTarget pOTarget, bool pbSayOther)
    {
        UWDoor lODoor = pOTarget.Door;

        if (lODoor != null)
        {
            // THE MODERN SCHEME'S AUTOMATIC KEY (part U4e step 2, the first game's UWWorldUse.fTryUseDoor): a carried key
            // that fits unlocks the closed door through the lock rule and names itself, then the door opens.
            DataImport lOData = mOLoader != null ? mOLoader.UWDataImporter : null;
            UWObject lOKey = lODoor.IsClosed && lODoor.IsLocked && UWWorldUse.KeyUsedAutomatically && mOScreen != null
                ? UWLockUseUw2.KeyAtHand(lODoor, mOScreen.Inventory, mOLoader) : null;

            if (lOKey != null && UWLockUseUw2.UnlockDoorWithKey(lOKey, lODoor))
            {
                if (lOData != null)
                    UWGameScreenUw2.Say(UWText.Sentence(lOData, "msg.door.unlocked_with", UWText.TheObject(lOData, lOKey.ID, UWGrammarCase.Dative)));
            }

            if (lODoor.IsLocked)
            {
                UWGameScreenUw2.Say(lODoor.Data != null && lOData != null
                    ? UWItemMessages.IsLocked(lOData, lODoor.Data.ID) : UWText.T("hud.uw2.door_locked"));
                UWAudioService.PlayEffect(LockedDoorSound);
            }
            else
                lODoor.Toggle(UWTrapRules.Setter.Player);

            // UseObj's checkTrap with mode 4 after the use, locked or not (G3 part H4).
            if (lODoor.Data != null)
                UWTriggerSystem.CheckTrapUw2(lODoor.Data, UWTrapRulesUw2.UseMode, lODoor.Data.TileX, lODoor.Data.TileY, mOLoader);

            return;
        }

        UWEntityInfo lOThing = pOTarget.Thing;
        UWObject lOObject = lOThing != null ? lOThing.ObjectData : null;

        // NOTHING UNDER THE POINTER (a wall, the floor): look_nothing with how 0, "You cannot use that." (per user
        // 2026-11-22: ours still said "Not yet in the walk through Ultima Underworld II.").
        if (lOObject == null)
        {
            if (pbSayOther)
            {
                DataImport lOData = mOLoader != null ? mOLoader.UWDataImporter : null;

                UWGameScreenUw2.Say(UWText.Block(lOData, 1, UWTalkRulesUw2.NothingFirstString + NothingUse + 1, string.Empty));
            }

            return;
        }

        // UseRect: the bed puts the player to sleep.
        if (lOObject.ID == UWSleepUw2.BedObjectId && UWSleepUw2.Current != null)
        {
            UWSleepUw2.Current.InBed();
            UWTriggerSystem.CheckTrapUw2(lOObject, UWTrapRulesUw2.UseMode, lOObject.TileX, lOObject.TileY, mOLoader);

            return;
        }

        // UseRect's switches, the lever and the switch picture, their use triggers with them.
        if (UWTriggerSystem.UseWallControlUw2(lOObject, mOLoader))
            return;

        // UseRect's minor 1 (uses part 2, 2026-11-18): a barrel or a chest not marked as a quantity is emptied as a
        // container (UseCont with how 0), and its owner, if it can have one, counts it once more (player_did_bad after
        // DumpTheBag's own; for a locked one too).
        if ((lOObject.ID == BarrelId || lOObject.ID == ChestId) && !lOObject.HasQuantity)
        {
            fDumpTheBag(lOThing);

            DataImport lOData = mOLoader.UWDataImporter;

            if (lOData != null && lOData.CommonObjectProperties != null
                && lOData.CommonObjectProperties.TryGet(lOObject.ID, out UWCommonObjectProperties.Entry lOEntry) && lOEntry.CanHaveOwner)
                UWTriggerSystem.PlayerDidBadUw2(lOObject.Owner, mOLoader, UWGameScreenUw2.Say);

            UWTriggerSystem.CheckTrapUw2(lOObject, UWTrapRulesUw2.UseMode, lOObject.TileX, lOObject.TileY, mOLoader);

            return;
        }

        // UseRune: the flam rune burns and explodes, the tym rune holds the player; it is gone either way.
        if (UWRuneRulesUw2.IsRune(lOObject.ID))
        {
            UWWorld lOWorld = UWWorld.Of(mOLoader);

            if (lOWorld != null && UWVitalsUw2.Current != null)
                UWRuneRulesUw2.Touch(lOObject, null, new UWSpellHostUw2(UWVitalsUw2.Current, mOLoader, lOWorld.Presentation));

            return;
        }

        // UseUtil in the world: the anvil and the pole ask for their target (UWToolUseUw2.UseInWorld).
        if (UWToolUseUw2.UseInWorld(lOObject, mOLoader.UWDataImporter, mOScreen != null ? mOScreen.Inventory : null))
        {
            UWTriggerSystem.CheckTrapUw2(lOObject, UWTrapRulesUw2.UseMode, lOObject.TileX, lOObject.TileY, mOLoader);

            return;
        }

        // UseCont in the world: a locked container says so, any other is emptied onto the floor (DumpTheBag; G4 rest part 3).
        if (lOObject.GetCategory() == UWObject.ObjectCategoryEnum.Containers)
        {
            fDumpTheBag(lOThing);
            UWTriggerSystem.CheckTrapUw2(lOObject, UWTrapRulesUw2.UseMode, lOObject.TileX, lOObject.TileY, mOLoader);

            return;
        }

        // UseMagic's fountain, also through the animation standing over it (UseObj's MAJOR_ANIMOBJ class 9, its next in
        // the chain the fountain; G4 rest part 2).
        UWObject lOFountain = fFountainOf(lOObject);

        if (lOFountain != null)
        {
            fUseFountain(lOFountain);
            UWTriggerSystem.CheckTrapUw2(lOObject, UWTrapRulesUw2.UseMode, lOObject.TileX, lOObject.TileY, mOLoader);

            return;
        }

        // UseBook in the world does nothing, its traps included (UseObj's trap flag is off for the whole class).
        if ((lOObject.ID & 0x1F0) == UWReadRulesUw2.FirstReadable)
            return;

        // UseRect's minor 2: any other wall picture goes to RectLook with look -1 (USEITEMS.C:913), which speaks only for
        // a bridge, a gravestone and a plaque (classes 4, 5, 6); a wall panel (14, 15) - a secret wall, per user
        // 2026-11-22 on UW2 level 52 at 32/43: ours said "You see ...", the original nothing - and the rest stay silent.
        if ((lOObject.ID & 0x1F0) == WallPictureClass)
        {
            int liInClass = lOObject.ID & 0xF;

            if (liInClass >= RectLookFirstSpeaking && liInClass <= RectLookLastSpeaking && UWAttackUw2.Current != null
                && UWAttackUw2.Current.Look != null)
                UWAttackUw2.Current.Look.LookAtTarget(lOThing, null);
        }
        // UseLight in the world: "Lights may only be used if equipped."
        else if (UWObjectMechanics.IsAnyLight(lOObject.ID))
            UWGameScreenUw2.Say(UWText.Block(mOLoader.UWDataImporter, 1, LightsEquippedString + 1, string.Empty));

        // ANY OTHER THING (2026-11-18: until then "Not yet in the walk"): UseObj's checkTrap with mode 4 and checkSpell,
        // the thing itself casting on the player without the wait between casts; nothing is said.
        UWTriggerSystem.CheckTrapUw2(lOObject, UWTrapRulesUw2.UseMode, lOObject.TileX, lOObject.TileY, mOLoader);

        if (UWGameScreenUw2.Current != null && mOLoader.CurrentLevel != null)
            UWCarriedSpellRulesUw2.CheckSpellInWorld(lOObject, mOLoader.CurrentLevel.Masterlist, UWGameScreenUw2.Current.CastOnPlayer);
    }

    /// <summary>RectLook's classes that speak when used (look -1): the bridge 4, the gravestone 5, the plaque 6.</summary>
    private const int RectLookFirstSpeaking = 4;

    private const int RectLookLastSpeaking = 6;

    /// <summary>"Lights may only be used if equipped." (UseLight with how 0, 0x89).</summary>
    private const int LightsEquippedString = 0x89;

    /// <summary>UseRect's minor 1 containers: the barrel (index 0xB) and the chest (0xD).</summary>
    private const int BarrelId = 0x15B;

    private const int ChestId = 0x15D;

    /// <summary>UseObj(ThePlayer, thing, 0) for a rule that works a thing in the world from afar (UsePoleOn's switch).</summary>
    public void UseThing(UWEntityInfo pOThing)
    {
        if (pOThing != null)
            fUseAt(new UseTarget { Thing = pOThing }, false);
    }

    /// <summary>
    /// UseCont and DumpTheBag (uw2 obj/USEITEMS.C:579, :816): a locked container "The &lt;name&gt; is locked."; else its whole
    /// chain goes down around it (drop_link_chain, UWSpill as the first game's), the owner of a container that can have
    /// one counts it as a bad deed (player_did_bad), and "You empty the &lt;name&gt;." or "The &lt;name&gt; is empty.".
    /// </summary>
    private void fDumpTheBag(UWEntityInfo pOEntity)
    {
        DataImport lOData = mOLoader != null ? mOLoader.UWDataImporter : null;
        UWObject lOBag = pOEntity.ObjectData;

        if (lOData == null || mOLoader.CurrentLevel == null)
            return;

        lOBag.EnsureContentsLoaded(mOLoader.CurrentLevel.Masterlist);

        if (UWSpillRules.FindLockedLock(lOBag.Contents) != null)
        {
            UWGameScreenUw2.Say(UWItemMessages.IsLocked(lOData, lOBag.ID));

            return;
        }

        int liOwner = lOData.CommonObjectProperties != null && lOData.CommonObjectProperties.TryGet(lOBag.ID, out UWCommonObjectProperties.Entry lOEntry)
            && lOEntry.CanHaveOwner ? lOBag.Owner : 0;
        int liDumped = UWSpill.Apply(mOLoader, lOBag, UWSpillRules.Partition(lOBag.Contents, UWSpillRules.LocksFreedOnSpill(UWGame.Uw2)), pOEntity.transform.position,
            mOLoader.WorldPositionToTile(pOEntity.transform.position), true);

        UWTriggerSystem.PlayerDidBadUw2(liOwner, mOLoader, UWGameScreenUw2.Say);
        UWGameScreenUw2.Say(liDumped > 0 ? UWItemMessages.Emptied(lOData, lOBag.ID) : UWItemMessages.IsEmpty(lOData, lOBag.ID));
    }

    /// <summary>
    /// A SMASHED CHEST OR BARREL (uw2 combat/DAMAGE.C: damage_object's checkTrap with mode 4, then remove_object; per user
    /// 2026-11-21, SAVE2 on level 3: the chest at 60/45 broke and nothing came out). remove_object frees the first lock
    /// (remove_lock(obj, 0)) and uses the chest with no one (UseCont(0, obj, 0)): a second lock still locked says "The
    /// chest is locked." and keeps the contents, else DumpTheBag spills them (its own remove_lock(cont, 1) first) and
    /// counts as theft from the owner; nothing is said. A chest with the door-direction bit only breaks. Until then the
    /// spill went through the first game's Interaction, which the second game's session does not have, and the contents
    /// went with the chest.
    /// </summary>
    public void SpillDestroyedContainer(UWEntityInfo pOEntity)
    {
        DataImport lOData = mOLoader != null ? mOLoader.UWDataImporter : null;
        UWObject lOBag = pOEntity != null ? pOEntity.ObjectData : null;

        if (lOData == null || lOBag == null || mOLoader.CurrentLevel == null)
            return;

        lOBag.EnsureContentsLoaded(mOLoader.CurrentLevel.Masterlist);

        UWTilePos lOTile = mOLoader.WorldPositionToTile(pOEntity.transform.position);

        UWTriggerSystem.CheckTrapUw2(lOBag, UWTrapRulesUw2.UseMode, lOTile.X, lOTile.Y, mOLoader);

        if (lOBag.DoorDirection)
            return;

        UWSpillRules.TakeFirstLock(lOBag.Contents);

        if (UWSpillRules.FindLockedLock(lOBag.Contents) != null)
        {
            UWGameScreenUw2.Say(UWItemMessages.IsLocked(lOData, lOBag.ID));

            return;
        }

        int liOwner = lOData.CommonObjectProperties != null && lOData.CommonObjectProperties.TryGet(lOBag.ID, out UWCommonObjectProperties.Entry lOEntry)
            && lOEntry.CanHaveOwner ? lOBag.Owner : 0;

        UWSpill.Apply(mOLoader, lOBag, UWSpillRules.Partition(lOBag.Contents, UWSpillRules.LocksFreedOnSpill(UWGame.Uw2)),
            pOEntity.transform.position, lOTile, true);
        UWTriggerSystem.PlayerDidBadUw2(liOwner, mOLoader, UWGameScreenUw2.Say);
    }

    /// <summary>The use triggers of a destroyed thing that is not used when destroyed (damage_object's checkTrap with
    /// mode 4).</summary>
    public void FireDestroyedUseTriggers(UWEntityInfo pOEntity)
    {
        UWObject lOThing = pOEntity != null ? pOEntity.ObjectData : null;

        if (lOThing == null || mOLoader == null)
            return;

        UWTilePos lOTile = mOLoader.WorldPositionToTile(pOEntity.transform.position);

        UWTriggerSystem.CheckTrapUw2(lOThing, UWTrapRulesUw2.UseMode, lOTile.X, lOTile.Y, mOLoader);
    }

    /// <summary>The fountain a used thing stands for: itself, or the fountain after a fountain's animation. Null else.</summary>
    private UWObject fFountainOf(UWObject pOObject)
    {
        if (pOObject.ID == UWToolRulesUw2.Fountain)
            return pOObject;

        if (!UWToolRulesUw2.IsFountainAnimation(pOObject.ID) || pOObject.Link == 0 || mOLoader == null || mOLoader.CurrentLevel == null)
            return null;

        List<UWObject> lOMaster = mOLoader.CurrentLevel.Masterlist;
        UWObject lONext = lOMaster != null && pOObject.Link < lOMaster.Count ? lOMaster[pOObject.Link] : null;

        return lONext != null && lONext.ID == UWToolRulesUw2.Fountain ? lONext : null;
    }

    /// <summary>
    /// UseMagic's fountain: dry at quality 0, else its line. A fountain's own spell (inanimate_spell with the fountain as
    /// the caster) is not cast: none of the shipped fountains carries one (UWToolRulesUw2), so it is only logged.
    /// </summary>
    private void fUseFountain(UWObject pOFountain)
    {
        DataImport lOData = mOLoader != null ? mOLoader.UWDataImporter : null;

        if (lOData == null)
            return;

        int liLine = UWToolRulesUw2.FountainLine(pOFountain, mOLoader.CurrentLevel != null ? mOLoader.CurrentLevel.Masterlist : null,
            out bool lbSpell, out int liMajor, out int liEffect);

        if (lbSpell)
            Debug.Log("[Use] a fountain's spell " + liMajor + "/" + liEffect + " is not cast (no shipped fountain carries one)");

        UWGameScreenUw2.Say(UWText.Block(lOData, 1, liLine, string.Empty));
    }

    /// <summary>The look of the session: the first game's picker and look (player_3dlook), the thing in front or the
    /// texture behind nothing; what the light's band does not draw is not there (Interaction.IsWithinSight).</summary>
    private void fLook()
    {
        Camera lOCamera = UWViewpoint.Current;

        if (lOCamera == null || UWAttackUw2.Current == null)
            return;

        UWWorldPicker.Measurement lOMeasured = fMeasureAtPointer(lOCamera);
        bool lbSeen = lOMeasured.HasFrontmost && Interaction.IsInSightBand(lOCamera.transform,
            lOMeasured.FrontmostHit.point + (lOMeasured.FrontmostHit.normal * 0.5f), fViewingDistance());
        UWEntityInfo lOThing = lbSeen ? lOMeasured.Frontmost : null;
        string lsChunk = lbSeen && lOThing == null
            ? Interaction.ChunkDescriptionAt(mOLoader != null ? mOLoader.UWDataImporter : null, lOMeasured.FrontmostHit) : null;

        fLookAt(lOThing, lsChunk);
    }

    /// <summary>player_3dlook on a thing (or a piece of the level): the look, the trap search, the look triggers.</summary>
    private void fLookAt(UWEntityInfo lOThing, string lsChunk)
    {
        if (UWAttackUw2.Current == null)
            return;

        UWAttackUw2.Current.Look.LookAtTarget(lOThing, lsChunk);

        // NO BookLook IN THE WORLD (per user 2026-11-18 against the original: a scroll on the floor says "You see a ..."
        // and is not read): player_3dlook gives the look its detail only within 0x48 AND for a thing that cannot be
        // picked up (!releaseable, INTERACT.C:421), and every book and scroll can be.

        // THE TRAP SEARCH (player_3dlook, G3 part H2): with Look lit on the command bar - or the modern look key, the
        // deliberate look as in the first game (UWUiSchemePolicy.TrapSearchNeedsLookMode) - a Search roll against
        // 10 + twice the world finds a trap in the thing's chain and asks whether to disarm it.
        if (lOThing != null && lOThing.ObjectData != null && mOScreen != null
            && (UWGameScreenUw2.Command == UWGameScreenUw2.CommandEnum.Look || !UWControlScheme.Ui.Policy.TrapSearchNeedsLookMode))
            fSearchForTrap(lOThing.ObjectData);

        // checkTrap with mode 5 after every look (player_3dlook): the thing's look triggers, a hidden one only for the
        // Search its z asks (G3 part H4).
        if (lOThing != null && lOThing.ObjectData != null)
            UWTriggerSystem.CheckTrapUw2(lOThing.ObjectData, UWTrapRulesUw2.LookMode, lOThing.ObjectData.TileX, lOThing.ObjectData.TileY,
                mOLoader, UWTrapRules.Setter.Player, fSearchSkill());
    }

    /// <summary>The player's Search, 0 without vitals.</summary>
    private static int fSearchSkill()
    {
        return UWVitalsUw2.Current != null && UWVitalsUw2.Current.Vitals != null
            ? UWVitalsUw2.Current.Vitals.GetSkill(UWPlayerData.Skill.Search) : 0;
    }

    /// <summary>DetectedTrap(thing, Search) &gt; 0: "You found a trap! Do you wish to try to disarm it?", and on yes
    /// RemoveTrap(thing, Traps) (UWTrapRulesUw2).</summary>
    private void fSearchForTrap(UWObject pOThing)
    {
        UWPlayerVitals lOVitals = UWVitalsUw2.Current != null ? UWVitalsUw2.Current.Vitals : null;
        UWLevel lOLevel = mOLoader != null ? mOLoader.CurrentLevel : null;

        if (lOVitals == null || lOLevel == null || UWGameScreenUw2.IsAskingYesNo)
            return;

        UWSkillCheck.ResultEnum? leFound = UWTrapRulesUw2.DetectTrap(pOThing, lOLevel.Masterlist,
            lOVitals.GetSkill(UWPlayerData.Skill.Search), mOLoader.CurrentLevelIndex + 1);

        if (!leFound.HasValue || !UWSkillCheck.IsSuccess(leFound.Value))
            return;

        mOScreen.AskYesNo(UWText.General(mOLoader.UWDataImporter, UWTrapRulesUw2.TrapFoundString), pbYes =>
        {
            if (pbYes && UWVitalsUw2.Current != null && UWVitalsUw2.Current.Vitals != null)
                UWTriggerSystem.RemoveTrapUw2(pOThing, UWVitalsUw2.Current.Vitals.GetSkill(UWPlayerData.Skill.Traps), mOLoader);
        });
    }

    /// <summary>The rows the light reaches (SHADES.DAT), as UWPlayerTerrain.ViewingDistance.</summary>
    private int fViewingDistance()
    {
        DataImport lOData = mOLoader != null ? mOLoader.UWDataImporter : null;

        return mOLighting != null && lOData != null && lOData.Shades != null && lOData.Shades.TryGet(mOLighting.LightLevel, out UWShades.Entry lOShade)
            ? lOShade.ViewingDistance : UWExplorationRules.RenderBandMaxDepth;
    }

    private void fFollowLight()
    {
        Camera lOCamera = UWViewpoint.Current;
        UWLevel lOLevel = mOLoader != null ? mOLoader.CurrentLevel : null;

        if (mODarkness == null || mOLighting == null || lOCamera == null || lOLevel == null)
            return;

        UWTilePos lOAt = UWTileQueries.WorldToTile(lOCamera.transform.position.x, lOCamera.transform.position.z);
        UWTile lOTile = lOLevel.GetTile(lOAt.X, lOAt.Y);
        int liTile = lOAt.IsOnMap ? lOAt.Index : -1;

        if (mOLoader.CurrentLevelIndex == miLastLevel && liTile == miLastTile)
            return;

        miLastLevel = mOLoader.CurrentLevelIndex;
        miLastTile = liTile;

        bool lbBit = lOTile != null && UWDarknessUw2.TileLightBit(lOTile.RawTileData);

        mOLighting.LevelMinimum = mODarkness.TileMinimum(mOLoader.CurrentLevelIndex, lbBit);
    }
}
