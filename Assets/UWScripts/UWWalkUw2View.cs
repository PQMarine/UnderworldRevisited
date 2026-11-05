using System.IO;
using UnityEngine;
using UWDataImport;
using UWDataImport.UWData;

/// <summary>
/// THE LIGHT OF THE SECOND GAME'S LEVELS ON ITS WALK (UW2 plan phase 2 step 3, 2026-10-29): DL.DAT
/// gives each level a minimum light, which the tile's light bit turns on or off
/// (UWDarknessUw2.TileMinimum, load_dl and change_GrSq). The tile is the
/// one under the camera; its minimum goes to UWLighting.LevelMinimum whenever the tile or the level
/// changes. The first game never has this component.
///
/// AND ITS DOORS (step 4): the walk has no interface to use things with, so the use key of the modern
/// scheme or a click opens and closes the door in front - the one the view's centre (or the free
/// pointer) points at within DoorReach. A locked door stays shut; no trigger of the door fires (its
/// traps are the second game's rules, phase 4).
/// </summary>
public class UWWalkUw2View : MonoBehaviour
{
    private UWDarknessUw2 mODarkness;

    private UWLighting mOLighting;

    private UWLevelLoader mOLoader;

    private int miLastLevel = -1;

    private int miLastTile = -1;

    /// <summary>The walk's view, for the modern HUD's target name.</summary>
    public static UWWalkUw2View Current { get; private set; }

    /// <summary>What the view's centre or the free pointer is on.</summary>
    public UWEntityInfo PointedEntity()
    {
        return fFirstEntity();
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

        // The walk leaves the level loader before its sound starts: without this the effects were the first
        // game's after its menu, or none at all.
        if (mOLoader != null && mOLoader.UWDataImporter != null)
            UWAudioService.Init(mOLoader.UWDataImporter);

        string lsData = mOLoader != null && mOLoader.UWDataImporter != null ? mOLoader.UWDataImporter.DataPath : null;
        string lsFile = lsData != null ? Path.Combine(lsData, UWDarknessUw2.FileName) : null;

        if (lsFile != null && File.Exists(lsFile))
            mODarkness = new UWDarknessUw2(File.ReadAllBytes(lsFile));
        else
            Debug.LogWarning("[Walk] No " + UWDarknessUw2.FileName + " - the levels have no minimum light.");

        // The game screen around the view (phase 3 step 1).
        if (mOLoader != null)
            mOScreen = UWGameScreenUw2.Attach(Camera.main, mOLoader.UWDataImporter);
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
        return UWControlScheme.SharedControls == null || UWSetupMenu.IsCapturingInput || UWGameScreenUw2.IsOptionsOpen || UWMapScreenUw2.IsOpen
            || UWConversationScreenUw2.IsOpen || UWGameScreenUw2.IsAskingCount || UWControls.IsTextEntryActive
            || (UWSleepUw2.Current != null && UWSleepUw2.Current.IsNight);
    }

    /// <summary>player_key_sleep.</summary>
    private void fCampByKey()
    {
        if (!fIsInputTaken() && UWControlScheme.SharedControls[UWAct.KeyCamp].WasPressedThisFrame() && UWSleepUw2.Current != null)
            UWSleepUw2.Current.Camp(mOScreen != null ? mOScreen.Inventory : null);
    }

    /// <summary>
    /// THE WALK SAVED into SAVE1 to SAVE4 under the save folder (phase 2 step 6; since phase 3 step 4 from
    /// the options' file page with the description typed there, UWGameScreenUw2): the inventory written
    /// into the walk's PLAYER.DAT first, then where the player stands, how he faces, on which level - the
    /// motion's fine position and z and the camera's yaw, as the original stores PN and PlayerFacing. Null,
    /// or what failed.
    /// </summary>
    public static string Save(int piSlot, string psDescription)
    {
        UWGameSession lOSession = UWGameSession.Current;
        UWLevelLoader lOLoader = UWScene.LevelLoader;
        UWPlayerMovement lOMovement = UWScene.Player != null ? UWScene.Player.GetComponent<UWPlayerMovement>() : null;
        string lsRoot = UnderworldRevisited.UWSettings.Instance != null ? UnderworldRevisited.UWSettings.Instance.SavegameRoot : null;
        string lsSlot = UWSavegameSlots.GetSlotPath(lsRoot, piSlot);

        if (lOSession == null || lOMovement == null || lOMovement.Motion == null || lsSlot == null || lOLoader == null)
            return "no walk to save";

        UWWorldSync.Capture(lOLoader);

        if (UWVitalsUw2.Current != null)
            UWVitalsUw2.Current.WriteToRecord();

        // The inventory as it is now into the walk's PLAYER.DAT (phase 3 step 3 part D).
        UWGameScreenUw2 lOScreen = UWGameScreenUw2.Current;
        string lsInventoryError = lOScreen != null ? lOScreen.WriteInventoryInto(lOSession.PlayerFileUw2) : null;

        if (lsInventoryError != null)
            return lsInventoryError;

        UWDataImport.UWData.UWMotionParams lOParams = lOMovement.Motion.Params;
        string lsError = UWSaveCodec.SaveUw2(lOSession, lsSlot, psDescription, lOLoader.CurrentLevelIndex, lOParams.X, lOParams.Y, lOParams.Z,
            lOMovement.Motion.CameraYaw);

        if (lsError == null)
        {
            // The bar's places beside the save, as the first game's (UWSavegameWriter).
            if (lOScreen != null)
                lOScreen.CaptureBar();

            UWHelpNotes.SaveTo(lsSlot);
        }

        if (lsError == null)
            Debug.Log(string.Format("[Walk] Saved into {0}: level {1}, tile {2}/{3}.", lsSlot, lOLoader.CurrentLevelIndex + 1, lOParams.X >> 8, lOParams.Y >> 8));

        return lsError;
    }

    /// <summary>
    /// THE VIEW'S BUTTONS (phase 3 step 1): the modern use key uses the door in front, as since step 4.
    /// On the classic screen the left button walks (UWPlayerInput, UWGameScreenUw2's window), and the
    /// right button does what the command bar has chosen, as mous_in_3d does with RightButtonThing: none
    /// or Look looks, Use uses a door, the rest are not in the walk yet. A click on the frame does
    /// nothing here.
    /// </summary>
    private void fUseDoor()
    {
        UWControls lOControls = UWControlScheme.SharedControls;

        // The options panel, the map and a conversation have the input while they are up.
        if (lOControls == null || fIsInputTaken() || (mOScreen != null && mOScreen.IsPanelOpen))
            return;

        UnityEngine.InputSystem.Mouse lOClickMouse = UnityEngine.InputSystem.Mouse.current;
        bool lbClassic = UWControlScheme.Shared != null && UWControlScheme.Shared.Current != UWControlScheme.SchemeEnum.Modern;
        bool lbInView = lOClickMouse != null && (mOScreen == null || mOScreen.IsInView(UWPointer.Position));
        bool lbRight = lbClassic && lbInView && !UWMouseButtons.IsBlocked && lOClickMouse.rightButton.wasPressedThisFrame;

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
                fDrop(lOInventory);
            else if (leCommand == UWGameScreenUw2.CommandEnum.None || leCommand == UWGameScreenUw2.CommandEnum.Look)
                fBeginLookPress();
            else if (leCommand == UWGameScreenUw2.CommandEnum.Get)
                fTake(fFirstEntity(), lOInventory, true);
            else if (leCommand == UWGameScreenUw2.CommandEnum.Use)
                fToggleDoor(true);
            else if (leCommand == UWGameScreenUw2.CommandEnum.Talk && mOScreen != null)
                mOScreen.TalkTo(fFirstEntity());
            else if (leCommand != UWGameScreenUw2.CommandEnum.Fight)
                UWGameScreenUw2.Say(UWText.T("hud.walk.not_yet"));

            return;
        }

        if (lOControls[UWAct.ModernUse].WasPressedThisFrame())
            fModernUse();
        else if (!lbClassic && lOControls[UWAct.ModernLook].WasPressedThisFrame())
            fLook();
    }

    /// <summary>The thing on the pointer goes into the world, from the modern bags.</summary>
    public void DropHeld(UWInventoryModel pOInventory)
    {
        fDrop(pOInventory);
    }

    /// <summary>The modern use key: a creature in front is talked to, what can be taken goes onto the pointer
    /// with the bags open, a door opens or closes; the rest of using is the second game's rules (phase 4).</summary>
    private void fModernUse()
    {
        UWEntityInfo lOThing = fFirstEntity();

        UWInventoryModel lOInventory = mOScreen != null ? mOScreen.Inventory : null;

        if (lOThing != null && (lOThing.ObjectData is UWNpc || UWTalkRulesUw2.IsWisp(lOThing.ObjectData)) && mOScreen != null)
            mOScreen.TalkTo(lOThing);
        else if (lOThing != null && lOThing.CanBePickedUp && lOInventory != null)
        {
            fTake(lOThing, lOInventory, true);

            if (lOInventory.CursorItem != null)
                mOScreen.OpenBags();
        }
        else
            fToggleDoor(true);
    }

    /// <summary>A right press with no command or Look: the look comes when the button comes up, unless
    /// the pointer is dragged off first - then the thing is taken ("look and drag to get",
    /// mous_in_3d's default).</summary>
    private bool mbLookPending;

    private Vector2 mOLookPressAt;

    private UWEntityInfo mOLookTarget;

    /// <summary>How far the pointer moves, in screen pixels, before a held right button takes.</summary>
    public const float DragToTakePixels = 6f;

    private void fBeginLookPress()
    {
        mbLookPending = true;
        mOLookPressAt = UWPointer.Position;
        mOLookTarget = fFirstEntity();
    }

    private void fFollowLookPress(UnityEngine.InputSystem.Mouse pOMouse, UWInventoryModel pOInventory)
    {
        if (pOMouse == null || !pOMouse.rightButton.isPressed)
        {
            mbLookPending = false;
            fLook();

            return;
        }

        if ((UWPointer.Position - mOLookPressAt).magnitude < DragToTakePixels)
            return;

        mbLookPending = false;

        if (mOLookTarget != null && mOLookTarget.CanBePickedUp)
            fTake(mOLookTarget, pOInventory, false);
    }

    /// <summary>The nearest thing with object data the pointer points at, within DoorReach.</summary>
    private UWEntityInfo fFirstEntity()
    {
        foreach (RaycastHit lOHit in fHits())
        {
            UWEntityInfo lOInfo = lOHit.collider.GetComponentInParent<UWEntityInfo>();

            if (lOInfo != null && lOInfo.ObjectData != null)
                return lOInfo;
        }

        return null;
    }

    /// <summary>
    /// TAKING A THING FROM THE WORLD (player_3dget, the same in both games but for its strings and the
    /// second game's pressure plates): something that can be taken and lies within the hand's reach
    /// (InPickRange, UWReachRules.CanReach; the terrain between is not tested yet) leaves its tile and
    /// goes onto the pointer, a container with its contents; a stack asks how many first. The theft, the
    /// moonstone and the pick-up trigger are the second game's rules (phase 4). pbSay: Get says what it cannot
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

        if (lOItem.GetCategory() == UWObject.ObjectCategoryEnum.Containers && mOLoader.CurrentLevel.Masterlist != null)
            lOItem.EnsureContentsLoaded(mOLoader.CurrentLevel.Masterlist);

        mOLoader.RemoveObjectFromWorld(lOItem);
        Destroy(pOEntity.gameObject);
        pOInventory.PickUpToCursor(lOItem);
    }

    /// <summary>The second game's "That is too far away to take." and "There is no space to drop that."
    /// in its block 1 (INTERACT.C player_3dget, MISSILE.C ReturnObject; the port's strings count one
    /// higher).</summary>
    public const int TooFarToTakeString = 0x6A;

    public const int NoSpaceToDropString = 0x10D;

    private const int TooHeavyString = 0x6C;

    private const int NothingGet = 3;

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
        fDropHeld(pOInventory);
    }

    /// <summary>punt_mouse_obj: what is on the pointer goes down.</summary>
    public void DropCursorItem()
    {
        UWInventoryModel lOInventory = UWGameScreenUw2.Current != null ? UWGameScreenUw2.Current.Inventory : null;

        if (lOInventory != null && lOInventory.CursorItem != null)
            fDropHeld(lOInventory);
    }

    private void fDropHeld(UWInventoryModel pOInventory)
    {
        DataImport lOData = mOLoader != null ? mOLoader.UWDataImporter : null;
        Camera lOCamera = UWViewpoint.Current;
        UWObject lOItem = pOInventory.CursorItem;

        if (lOData == null || lOCamera == null || lOItem == null)
            return;

        if (fTryThrow(pOInventory, lOItem, lOCamera.transform))
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
    }

    private bool fTryThrow(UWInventoryModel pOInventory, UWObject pOItem, Transform pOCamera)
    {
        UnityEngine.InputSystem.Mouse lOMouse = UnityEngine.InputSystem.Mouse.current;

        if (UWGameScreenUw2.Current == null || UWAttackUw2.Current == null)
            return false;

        Vector2 lOPointer = UWGameScreenUw2.IsClassicActive || lOMouse == null ? UWPointer.Position : lOMouse.position.ReadValue();

        UWGameScreenUw2.Current.PointerInMissileWindow(lOPointer, out int liX, out int liY);

        if (!UWPlayerThrow.TryThrowUw2(pOItem, liX, liY, pOCamera, mOLoader, UWAttackUw2.Current))
            return false;

        pOInventory.ReleaseCursorItem();
        pOInventory.ApplyCarriedLight();

        return true;
    }

    /// <summary>What the view's centre or the free pointer points at within DoorReach, nearest first.</summary>
    private RaycastHit[] fHits()
    {
        Camera lOCamera = UWViewpoint.Current;

        if (lOCamera == null)
            return new RaycastHit[0];

        RaycastHit[] lOHits = Physics.RaycastAll(fPointerRay(lOCamera), DoorReach, ~0, QueryTriggerInteraction.Collide);
        System.Array.Sort(lOHits, (pOA, pOB) => pOA.distance.CompareTo(pOB.distance));

        return lOHits;
    }

    /// <summary>The classic screen captures the system mouse: our pointer says where it points
    /// (UWPointer.Position); the modern scheme's locked look aims through the middle.</summary>
    private static Ray fPointerRay(Camera pOCamera)
    {
        UnityEngine.InputSystem.Mouse lOMouse = UnityEngine.InputSystem.Mouse.current;

        return UWGameScreenUw2.IsClassicActive
            ? pOCamera.ScreenPointToRay(UWPointer.Position)
            : Cursor.lockState != CursorLockMode.Locked && lOMouse != null
            ? pOCamera.ScreenPointToRay(lOMouse.position.ReadValue())
            : pOCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
    }

    /// <summary>Opens or closes the door pointed at; a locked one says so in the scroll. pbSayOther: a
    /// use of something else says it is not in the walk yet.</summary>
    private void fToggleDoor(bool pbSayOther)
    {
        foreach (RaycastHit lOHit in fHits())
        {
            UWDoor lODoor = lOHit.collider.GetComponentInParent<UWDoor>();

            if (lODoor == null)
                continue;

            if (lODoor.IsLocked)
                UWGameScreenUw2.Say(UWText.T("hud.walk.door_locked"));
            else
                lODoor.Toggle(UWTrapRules.Setter.Player);

            return;
        }

        UWEntityInfo lOThing = fFirstEntity();

        // UseRect: the bed puts the player to sleep.
        if (lOThing != null && lOThing.ObjectData != null && lOThing.ObjectData.ID == UWSleepUw2.BedObjectId && UWSleepUw2.Current != null)
        {
            UWSleepUw2.Current.InBed();

            return;
        }

        if (pbSayOther)
            UWGameScreenUw2.Say(UWText.T("hud.walk.not_yet"));
    }

    /// <summary>The look of the walk: the first game's picker and look (player_3dlook), the thing in front or the
    /// texture behind nothing; what the light's band does not draw is not there (Interaction.IsWithinSight).</summary>
    private void fLook()
    {
        Camera lOCamera = UWViewpoint.Current;

        if (lOCamera == null || UWAttackUw2.Current == null)
            return;

        UWWorldPicker.Measurement lOMeasured = UWWorldPicker.Measure(fPointerRay(lOCamera));
        bool lbSeen = lOMeasured.HasFrontmost && Interaction.IsInSightBand(lOCamera.transform,
            lOMeasured.FrontmostHit.point + (lOMeasured.FrontmostHit.normal * 0.5f), fViewingDistance());
        UWEntityInfo lOThing = lbSeen ? lOMeasured.Frontmost : null;
        string lsChunk = lbSeen && lOThing == null
            ? Interaction.ChunkDescriptionAt(mOLoader != null ? mOLoader.UWDataImporter : null, lOMeasured.FrontmostHit) : null;

        UWAttackUw2.Current.Look.LookAtTarget(lOThing, lsChunk);
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
