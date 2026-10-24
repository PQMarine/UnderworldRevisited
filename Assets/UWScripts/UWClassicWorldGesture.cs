using UnityEngine;
using UnityEngine.InputSystem;
using UWDataImport.UWData;

/// <summary>
/// THE CLASSIC SCHEME'S CLICKS INTO THE WORLD (cleanup step 5.14, 2026-10-22: out of Interaction,
/// which keeps the public surface and forwards): the right button's gesture in the view window -
/// the press measured once (UWWorldPicker), what it does decided by UWClickRules for the command
/// mode (look, get, use, talk) and the kind of target and carried out here (fApply), the drag
/// threshold that puts a pickable thing on the pointer, the release -, the use mode's right click
/// into the view window, and the arrows and the disc of the compass. Interaction ticks it in its
/// Update's order; the modern scheme's world input is UWModernWorldInput.
/// </summary>
public sealed class UWClassicWorldGesture
{
    private readonly Interaction mOInteraction;

    /// <summary>The right button in the 3D view: pressed once, read once - see UWWorldPointer.</summary>
    private readonly UWWorldPointer mOPointer = new UWWorldPointer();

    /// <summary>The open right-button gesture - what the press decided it to be (UWClickRules).</summary>
    private UWClickRules.GestureKind meGesture;

    public UWClassicWorldGesture(Interaction pOInteraction)
    {
        mOInteraction = pOInteraction;
    }

    private UWInventory fInventory()
    {
        return mOInteraction.Inventory;
    }

    private UWGameUI fUi()
    {
        return mOInteraction.GameUi;
    }

    /// <summary>The right button's open gesture ends without acting - the weapon's release takes
    /// the button (UWPlayerAttackInput).</summary>
    public void CancelPointer()
    {
        mOPointer.Cancel();
    }

    /// <summary>A command icon was chosen (Interaction.SetCommandMode): a gesture of the right
    /// button that is still open is cancelled.</summary>
    public void Cancel()
    {
        mOPointer.Cancel();
        meGesture = UWClickRules.GestureKind.None;
    }

    /// <summary>
    /// The press: what lies under the pointer is measured ONCE - the frontmost thing (or the
    /// wall or floor texture), and the pickable thing behind a blood pool or bone pile (one
    /// UWWorldPicker measurement). Until 2026-09-18 UWItemDrag ran its own raycast and its own click
    /// threshold on the same button, and the two disagreed on small movements.
    /// </summary>
    public void Begin()
    {
        Vector2 lOMousePos = Mouse.current.position.ReadValue();
        UWWorldPointer.Aim lOAim = new UWWorldPointer.Aim { PressPosition = lOMousePos };

        // With Original, outside the small 3D view window (e.g. right click on the backpack or
        // paper doll panel, see UWItemDrag) no world raycast - the UI does not block the physics
        // query, a raycast from there would hit invisible geometry behind the UI. The modern
        // view fills the screen, nothing lies outside it (IUWUiScheme.IsPointerOutsideView).
        bool lbOutsideGameArea = UWControlScheme.Ui.IsPointerOutsideView(lOMousePos);

        // BEYOND THE SIGHT NOTHING IS THERE: the original draws only as deep as the light allows,
        // and what is not drawn cannot be aimed at - neither looked at nor used (see
        // IsWithinSight). The aim then stays empty and the look answers "You see nothing.".
        if (!lbOutsideGameArea)
        {
            UWWorldPicker.Measurement lOMeasured = UWWorldPicker.Measure(mOInteraction.InteractionRay());

            if (lOMeasured.HasFrontmost && mOInteraction.IsWithinSight(lOMeasured.FrontmostHit))
            {
                lOAim.Frontmost = lOMeasured.Frontmost;
                lOAim.ChunkDescription = lOAim.Frontmost == null ? mOInteraction.DescribeChunkHit(lOMeasured.FrontmostHit) : null;
                lOAim.Pickable = lOMeasured.Pickable;
            }
        }

        mOPointer.Begin(lOAim);

        // WHAT THE PRESS DOES is a rule, not code here: UWClickRules knows the mode, the kind of
        // target and whether the press was in the viewport, and says what to do and whether the
        // gesture stays open (measured by the user on the original, 2026-09-18 and 19).
        fApply(UWClickRules.OnPress(mOInteraction.CommandMode, fClassify(lOAim), !lbOutsideGameArea), lOAim);
    }

    /// <summary>The kind of thing under the pointer, as UWClickRules tells them apart. A
    /// pickable thing counts as such even behind a blood pool or a bone pile.</summary>
    private static UWClickRules.TargetKind fClassify(UWWorldPointer.Aim pOAim)
    {
        if (pOAim.Pickable != null)
            return UWClickRules.TargetKind.Pickable;

        if (pOAim.Frontmost == null)
            return pOAim.ChunkDescription != null ? UWClickRules.TargetKind.Geometry : UWClickRules.TargetKind.Nothing;

        if (Interaction.IsGeometryEntity(pOAim.Frontmost))
            return UWClickRules.TargetKind.Geometry;

        return Interaction.IsCreatureEntity(pOAim.Frontmost) ? UWClickRules.TargetKind.Creature : UWClickRules.TargetKind.Thing;
    }

    /// <summary>Carries out what UWClickRules decided, on the aim of the press. The actions are
    /// the public ones of Interaction - the command icons of the original call the same.</summary>
    private void fApply(UWClickRules.Decision pODecision, UWWorldPointer.Aim pOAim)
    {
        switch (pODecision.Action)
        {
            case UWClickRules.ActionKind.Look:
                mOInteraction.LookAtTarget(pOAim.Frontmost, pOAim.ChunkDescription);
                break;

            case UWClickRules.ActionKind.Message:
                mOInteraction.AddGeneralMessage(pODecision.MessageIndex);
                break;

            case UWClickRules.ActionKind.ConversationMessage:
                mOInteraction.AddConversationBlockMessage(pODecision.MessageIndex);
                break;

            case UWClickRules.ActionKind.TakeToPointer:
            case UWClickRules.ActionKind.BeginDrag:
                // Both put the thing onto the pointer within the hand's reach ("That is too far
                // away to take." otherwise); the drag of the default state additionally marks the
                // gesture as used up, so the release does not use the thing as well.
                if (pOAim.Pickable != null && UWScene.ItemDrag != null)
                {
                    if (!mOInteraction.TryReachForPickup(pOAim.Pickable))
                        mOPointer.DragConsumed = true;
                    else if (UWScene.ItemDrag.TryBeginWorldDrag(pOAim.Pickable))
                        mOPointer.DragConsumed = true;
                }
                break;

            case UWClickRules.ActionKind.Talk:
                mOInteraction.TalkTo(pOAim.Frontmost);
                break;

            case UWClickRules.ActionKind.TalkToThing:
                mOInteraction.TalkToThing(pOAim.Frontmost ?? pOAim.Pickable);
                break;

            case UWClickRules.ActionKind.Use:
                mOInteraction.UseTarget(pOAim.Frontmost);
                break;
        }

        meGesture = pODecision.Gesture;

        if (meGesture == UWClickRules.GestureKind.None)
            mOPointer.Cancel();
    }

    /// <summary>
    /// The gesture after the press: the drag threshold and the release, both answered by
    /// UWClickRules (which knows the gesture the press opened). The threshold decides (checked
    /// by user in the original, 2026-09-18): from the sixth pixel a pickable thing follows the
    /// pointer at once, anything else is used ON THE RELEASE after the threshold was crossed;
    /// within the threshold one may jitter. A remembered creature or thing (talk and use mode)
    /// is acted on by any release.
    /// </summary>
    public void UpdatePointer()
    {
        if (!mOPointer.IsPressed)
            return;

        // Combat mode switched on, or something put on the pointer, while the button was held:
        // the gesture is over, the release must not use anything.
        if (mOInteraction.IsCombatModeActive || (fInventory() != null && fInventory().CursorItem != null))
        {
            mOPointer.Cancel();
            meGesture = UWClickRules.GestureKind.None;
            return;
        }

        UWWorldPointer.Aim lOAim = mOPointer.Current;

        UWWorldPointer.EventKind leEvent = mOPointer.Poll(Mouse.current.position.ReadValue(),
            mOInteraction.Input.Interact.WasReleasedThisFrame());

        // Modern has no dragging, its pickup sits in UseTarget.
        if (leEvent == UWWorldPointer.EventKind.DragStarted && UWControlScheme.Ui.Policy.WorldDragPicksUp)
            fApply(UWClickRules.OnDragThreshold(meGesture, fClassify(lOAim)), lOAim);

        // A drag that starts and ends in the same frame is a release after a drag too.
        bool lbReleased = leEvent == UWWorldPointer.EventKind.ReleasedAsClick
            || leEvent == UWWorldPointer.EventKind.ReleasedAfterDrag
            || (leEvent == UWWorldPointer.EventKind.DragStarted && !mOPointer.IsPressed);

        if (lbReleased)
        {
            fApply(UWClickRules.OnRelease(meGesture, leEvent != UWWorldPointer.EventKind.ReleasedAsClick,
                mOPointer.DragConsumed), lOAim);
        }
    }

    /// <summary>Use mode: the right button in the view window puts the held item onto what is
    /// there, UWItemDrag applies it (and keeps the clicks on the inventory - the anvil and the
    /// oil flask go the other way round). Outside the view window nothing happens here.</summary>
    public void UpdateUseModeInWorld()
    {
        if (UWPlayerInput.ConsumesRightClick() || !mOInteraction.Input.Interact.WasPressedThisFrame() || UWScene.ItemDrag == null)
            return;

        Vector2 lOMousePos = Mouse.current.position.ReadValue();

        if (fUi() != null && !fUi().ClassicScreen.IsScreenPositionInGameArea(lOMousePos))
            return;

        UWScene.ItemDrag.ApplyUseModeItemInWorld(lOMousePos);
    }

    /// <summary>
    /// A click on one of the three arrows under the compass: the easy movement
    /// (UWEasyMovement, UWHudCompass.TryGetEasyMovementCommand). Both mouse buttons do it, as
    /// with the compass itself - the original's region handler does not distinguish them
    /// either. Returns true if the click is used up.
    /// </summary>
    public bool TryEasyMovement(Vector2 pOScreenPos)
    {
        if (fUi() == null || !fUi().Compass.TryGetEasyMovementCommand(pOScreenPos, out int liCommand))
            return false;

        UWPlayerMovement lOMovement = UWScene.PlayerMovement;

        if (lOMovement != null)
            lOMovement.Input.HoldEasyMovement(liCommand);

        return true;
    }

    /// <summary>
    /// A click on the compass prints the character's status - hunger, fatigue,
    /// level, day and time of day (see UWStatusReport).
    ///
    /// BOTH MOUSE BUTTONS do the same. The reference does not distinguish them
    /// (uimanager_compass._on_compass_click only checks THAT a button was pressed).
    ///
    /// Returns true if the click is used up - then it must not trigger anything else.
    /// </summary>
    public bool TryReportStatus(Vector2 pOScreenPos)
    {
        if (fUi() == null || !fUi().Compass.IsScreenPositionOnCompass(pOScreenPos))
            return false;

        fUi().Actions.ReportStatus();

        return true;
    }
}
