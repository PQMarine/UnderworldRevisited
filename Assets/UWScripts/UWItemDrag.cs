using UnityEngine;
using UnityEngine.InputSystem;
using UWDataImport;
using UWDataImport.UWData;

/// <summary>
/// Original controls, backpack/equipment slots: click+hold (left OR right, both
/// equivalent) on an occupied backpack slot or an equipped piece starts a
/// drag as soon as the mouse has moved a little afterwards (see DragState.Pending/
/// ClickMoveThreshold - before this threshold nothing has happened yet, the item is
/// still in its place). Only when the threshold is exceeded does the item vanish
/// IMMEDIATELY from its source slot and move into UWInventory.CursorItem (not just
/// hidden visually: it also has no weight while being dragged, confirmed in the
/// original). Releasing over the matching paperdoll body area or a
/// backpack slot places it there.
///
/// If the target is already occupied, the items are swapped instead of ending the drag - the item
/// that was there then hangs on the pointer and the drag continues seamlessly (confirmed in the original).
///
/// An invalid drop (wrong armour type, or no recognised target at all) does NOT cancel the
/// drag - the item keeps "sticking" to the pointer (only the held button is
/// released, see DragState.StickyDrag) until a later click hits a valid target - the right button
/// anywhere, the left button only over the UI: in the view window the left button walks and
/// never throws the item (per user on the original, 2026-09-18).
///
/// If the button is released BEFORE the movement threshold was exceeded, it counts
/// as a simple click instead of a drag - each button with its own meaning: right click =
/// look (the item name is printed, like a world look target). Left click = open a
/// container, read, use the item immediately, or start "use" mode (the cursor carries the
/// item to apply it to a target), see fReleasePending and UWClassicPanelClick.
///
/// World pickup (original): right-click drag on a pickable object directly in the
/// 3D world (not backpack/paperdoll) works the same way - only with the right mouse button,
/// since the left one controls movement there (see UWPlayerInput.fBuildOriginalCoreCommand).
/// A simple right click (no drag) on such an object only shows its name,
/// just like with a backpack item - the actual pickup only happens on an
/// actual drag. Modern pickup (instant via right click, no drag) stays
/// unchanged in Interaction.cs.
///
/// Drop/throw (original): releasing (or a sticky right click) in the 3D view window itself
/// is also a valid target, see UWClassicDrop.fTryDropOrThrowInWorld and UWPlayerThrow - the
/// lower third of the window drops in front of the player, above it the item is thrown as a
/// projectile.
///
/// Manual hit testing (RectTransformUtility) instead of Unity UI events, same convention
/// as the rest of the project (see UWPointerZones, UWModernBags) - there is
/// deliberately no EventSystem in the scene.
///
/// SPLIT IN CLEANUP STEP 5.15 (2026-10-22): the drag's state is one DragState and the press's
/// source one UWDragSource (engine-free, it takes, returns and consumes); the conversation's
/// trade areas are UWClassicTradeDrag, the use mode UWClassicUseMode, the host of each item use
/// UWItemUseHost, where the thing lands UWClassicDrop, the look and the use of a click that did
/// not become a drag UWClassicPanelClick, the pointer picture and the hiding of the system pointer
/// UWDragIcon, the count prompt UWCountPrompt (step 5.13). This class keeps the press and the
/// drag.
/// </summary>
public class UWItemDrag : MonoBehaviour
{
    private const float ClickMoveThreshold = UWWorldPointer.DragThreshold;

    /// <summary>
    /// THE STATE OF THE CLASSIC DRAG (cleanup step 5.15, 2026-10-22: one value instead of the bools
    /// mbIsPending, mbIsDragging and mbIsHeld beside the count prompt's and the model's flags).
    /// </summary>
    public enum DragState
    {
        /// <summary>Nothing pressed, nothing on the pointer.</summary>
        Idle,

        /// <summary>Button pressed, source already determined, but the movement threshold has not
        /// yet been exceeded - the actual drag (removal from the inventory, see fBeginActualDrag)
        /// has not started yet.</summary>
        Pending,

        /// <summary>"Move how many?" (UWCountPrompt) - the rest of the drag flow does not run.</summary>
        CountPrompt,

        /// <summary>The thing on the pointer with the button that took it still held.</summary>
        HeldDrag,

        /// <summary>The thing "sticks" to the pointer: an invalid drop, a swap, a count taken or a
        /// thing handed over released the button - every further click tries again to place it.</summary>
        StickyDrag,

        /// <summary>A thing in use hangs on the pointer as its icon (UWClassicUseMode) - the
        /// inventory model's UseModeItem, which Interaction reads too.</summary>
        UseMode
    }

    private UWInventory mOInventory;
    private UWControlScheme mOControlScheme;
    private UWGameUI mOGameUi;
    private Interaction mOInteraction;
    private UWControls mOControls;
    private DataImport mOUWData;

    /// <summary>The thing's icon on the pointer and the hiding of the system pointer for it.</summary>
    private UWDragIcon mODragIcon;

    /// <summary>The conversation's trade areas as sources and places of the drag.</summary>
    private UWClassicTradeDrag mOTrade;

    /// <summary>The use mode: a thing in use hangs on the pointer until its target is clicked.</summary>
    private UWClassicUseMode mOUseMode;

    /// <summary>Where the thing on the pointer lands (UWClassicDrop).</summary>
    private UWClassicDrop mODrop;

    /// <summary>The click on a carried thing that did not become a drag (UWClassicPanelClick).</summary>
    private UWClassicPanelClick mOClick;

    /// <summary>What every item use shares (UWItemUseHost.Services) - each use gets its own host.</summary>
    private UWItemUseHost.Services mOUseServices;

    /// <summary>The drag's own state - Idle, Pending, CountPrompt, HeldDrag or StickyDrag; the use
    /// mode is the model's (see State).</summary>
    private DragState meState;

    private Vector2 mOPendingStartPos;

    /// <summary>The button of the press - its release ends the pending click and the held drag.</summary>
    private bool mbUsingRightButton;

    /// <summary>Source slot of the item currently dragged/targeted - during Pending used for the
    /// "look"/click feedback (the item is still there), during the drag only
    /// for the case of a final cancel (see fCancelDrag/
    /// UWInventoryModel.ReturnCursorItem). After a swap (see fEndDragAfterPlacement) the
    /// origin of the NEW cursor item is unknown and the source is None - a
    /// cancel then falls back to the first free backpack slot.</summary>
    private UWDragSource mOSource;

    /// <summary>The pending click carries the USE - decided once on the press by fIsUseClick;
    /// everything that reacts to a use click asks this, never the button.</summary>
    private bool mbPendingIsUseClick;

    /// <summary>The state of the drag: the count prompt, then the use mode (the model's), then the
    /// drag's own - in the order Update asks them.</summary>
    public DragState State
    {
        get
        {
            if (meState != DragState.CountPrompt && mOInventory != null && mOInventory.UseModeItem != null)
                return DragState.UseMode;

            return meState;
        }
    }

    /// <summary>For UWPlayerInput: during an original-style drag, crossing
    /// the 3D view window must not also count as cursor movement.</summary>
    public bool IsDragging
    {
        get { return meState == DragState.HeldDrag || meState == DragState.StickyDrag; }
    }

    /// <summary>The drag with the button still held - only then does the left button belong to
    /// the drag. Once the item merely sticks to the pointer, the left button walks again (per
    /// user on the original, 2026-09-18).</summary>
    public bool IsHeldDrag
    {
        get { return meState == DragState.HeldDrag; }
    }

    private void Awake()
    {
        mOInventory = GetComponent<UWInventory>();

        if (mOInventory == null)
            mOInventory = gameObject.AddComponent<UWInventory>();

        mOControlScheme = GetComponentInParent<UWControlScheme>();
        mOGameUi = GetComponent<UWGameUI>();
        mOInteraction = GetComponent<Interaction>();
        mOTrade = new UWClassicTradeDrag(this);
        mOUseServices = new UWItemUseHost.Services(this, mOInventory, mOGameUi, mOInteraction);
        mODrop = new UWClassicDrop(transform, mOInventory, mOGameUi, mOInteraction, mOControlScheme, mOTrade, mOUseServices);
        mOClick = new UWClassicPanelClick(this, mOInventory, mOGameUi, mOInteraction, mOTrade, mOUseServices);
    }

    private void Start()
    {
        if (mOControlScheme != null)
            mOControls = mOControlScheme.Controls;

        UWLevelLoader lOLoader = UWScene.LevelLoader;

        if (lOLoader != null)
            mOUWData = lOLoader.UWDataImporter;

        mOUseServices.Data = mOUWData;
        mODragIcon = new UWDragIcon(mOGameUi);
        mOUseMode = new UWClassicUseMode(mOInventory, mOInteraction, mOGameUi, mOControlScheme, mODragIcon, mOUWData,
            () => IsAskingCount);

        // Changing the object number along with the image lives here; the inventory only reports that
        // a light source has burned out.
        if (mOInventory != null)
            mOInventory.LightBurnedOut += fOnLightBurnedOut;
    }

    private void OnDestroy()
    {
        if (mOInventory != null)
            mOInventory.LightBurnedOut -= fOnLightBurnedOut;

        // The drag canvas lives at the root (UWDragIcon) and goes with this component.
        if (mODragIcon != null)
            mODragIcon.Destroy();
    }

    /// <summary>A burning light source is used up: it turns back into its
    /// unlit counterpart.</summary>
    private void fOnLightBurnedOut(UWObject pOItem)
    {
        UWObjectMechanics.Extinguish(pOItem, mOUWData != null ? mOUWData.Textures : null);
    }

    private void Update()
    {
        if (mOControls == null && mOControlScheme != null)
            mOControls = mOControlScheme.Controls;

        if (mOControls == null || mOGameUi == null)
            return;

        // The open map is modal - no drag starts behind it.
        if (mOGameUi.Map.IsMapVisible)
            return;

        // Nor during a cutscene. Look, movement and
        // interaction have long checked the same switch, the inventory was the only one
        // that did not (noticed by the user during the dream, 2026-09-07). IN A CONVERSATION, however,
        // the inventory stays usable, as in the original (see
        // UWControlScheme.IsInventoryInputBlocked).
        if (mOControlScheme != null && mOControlScheme.IsInventoryInputBlocked)
            return;

        // While a projectile spell is waiting for its target, the inventory is silent: in the original
        // neither the left nor the right button does anything there (per user, 2026-09-05).
        // WALKING is unaffected by this, that lives in UWPlayerMovement.
        //
        // A TARGET SPELL, however, can be discharged here - and if it misses, the
        // click then does what it would otherwise do (see
        // UWClassicPanelClick.TryResolveTargetSpell).
        //
        // ONLY IN THE CLASSIC SCHEME: its inventory is not on screen in the modern one, yet its
        // slots were still hit - a wand's Name Enchantment went onto an invisible backpack slot
        // under the modern bags and was gone (per user, 2026-10-04). The modern bags take it.
        bool lbClassicScheme = UWControlScheme.Ui.Policy.InventoryOnFrame;

        if (lbClassicScheme && mOInteraction != null && mOInteraction.IsSpellTargeting
            && !mOClick.TryResolveTargetSpell(mOControls))
            return;

        // While a long text is paging, everything here is silent: the click used
        // to turn the page must not start a drag. The per-frame check is
        // needed because the order of the Update calls is not fixed - on a click on
        // the LAST page, Interaction would otherwise already be done before we get here.
        if (mOInteraction != null
            && (mOInteraction.IsWaitingForPage || mOInteraction.PageInputConsumedThisFrame))
            return;

        // During the quantity prompt the rest of the drag flow does not run - otherwise
        // the click used to answer would immediately start the next drag.
        if (fUpdateCountPrompt())
            return;

        if (!UWControlScheme.Ui.Policy.InventoryOnFrame)
        {
            if (IsDragging)
                fCancelDrag();
            else if (meState == DragState.Pending)
                meState = DragState.Idle; // Item was never removed - just forget it.

            if (mOUseMode.IsActive)
                mOUseMode.End();

            return;
        }

        switch (State)
        {
            case DragState.UseMode:
                mOUseMode.Update(mOControls);
                return;

            case DragState.Pending:
                fUpdatePending();
                return;

            case DragState.Idle:
                fUpdateIdle();
                return;

            default:
                fUpdateDrag();
                return;
        }
    }

    /// <summary>Nothing pressed: a thing on the pointer becomes a sticky drag, a press starts a
    /// pending click.</summary>
    private void fUpdateIdle()
    {
        // If something hangs on the pointer without a drag running, it becomes a sticky drag: this is how
        // an item that a conversation partner hands over (take_from_npc, as in the
        // original) gets to its place - and likewise whatever a cancelled drag with a full
        // backpack left on the pointer. Before, it was stuck there invisibly (per user,
        // 2026-09-11: "it sticks to the cursor").
        if (mOInventory.CursorItem != null)
        {
            fBeginActualDrag();
            fLetGoOfButton();
            return;
        }

        if (UWMouseButtons.LeftPressed)
            fTryStartPending(false);
        else if (mOControls.Player.Interact.WasPressedThisFrame() && !UWPlayerInput.ConsumesRightClick())
            fTryStartPending(true);
    }

    /// <summary>The thing on the pointer, held or sticking.</summary>
    private void fUpdateDrag()
    {
        Vector2 lOMousePos = fPointerPosition();
        mODragIcon.MoveTo(lOMousePos);

        if (meState == DragState.HeldDrag)
        {
            // Held: the button that started the drag must be released.
            InputAction lOButtonAction = mbUsingRightButton ? mOControls.Player.Interact : mOControls.Player.CursorDrag;

            if (lOButtonAction.WasReleasedThisFrame())
                fEndHeldDrag(lOMousePos);
        }
        else
        {
            // "Sticks" to the pointer (an invalid drop did not cancel the drag, see
            // fEndHeldDrag) - every further click tries again to place it, without having to
            // hold the button again: the right button anywhere, the left button only over the
            // UI - in the view window the left button stays cursor movement and never throws
            // the item (per user on the original, 2026-09-18; until then either button placed it
            // anywhere). In a conversation the parchment covers the view, so all is UI.
            bool lbOverUi = !mOGameUi.ClassicScreen.IsScreenPositionInGameArea(lOMousePos)
                || (mOControlScheme != null && mOControlScheme.IsConversationOpen);

            if (mOControls.Player.Interact.WasPressedThisFrame()
                || (lbOverUi && UWMouseButtons.LeftPressed))
            {
                if (mODrop.TryPlaceAt(lOMousePos))
                    fEndDragAfterPlacement();
            }
        }
    }

    /// <summary>The button that took the thing is let go: a held drag goes on sticking.</summary>
    private void fLetGoOfButton()
    {
        if (meState == DragState.HeldDrag)
            meState = DragState.StickyDrag;
    }

    /// <summary>
    /// WHICH BUTTON CARRIES THE USE OVER THE UI - the one place that decides it. The left
    /// button always; the right one in the use mode, where over the UI it replaces the look
    /// (per user on the original, 2026-09-19) - but not in a conversation, where the function
    /// stays the usual one even with the mode lit (per user, the same day). Opening and
    /// closing a container, the scroll arrows, starting use mode on a key or the pole, the
    /// combat toggle on the weapon hand: all of them ask mbPendingIsUseClick.
    /// </summary>
    private bool fIsUseClick(bool pbRightButton)
    {
        return UWClickRules.IsUseClick(pbRightButton,
            mOInteraction != null ? mOInteraction.CommandMode : UWCommandMode.None,
            mOTrade.IsConversationOpen);
    }

    /// <summary>Checks whether there is a pickable item under the mouse at all, and remembers
    /// its source - the actual drag only begins in fUpdatePending, once the
    /// movement threshold is exceeded.</summary>
    private void fTryStartPending(bool pbRightButton)
    {
        Vector2 lOMousePos = fPointerPosition();

        // Always reset the source before exactly one (if any is hit at all) is set anew.
        mOSource = UWDragSource.None;

        // Decided FIRST, before any of the sources below - the trade area asks it on the
        // release as well (per user, 2026-09-19: a left click there must mark, not look).
        mbPendingIsUseClick = fIsUseClick(pbRightButton);

        // The trade areas of the conversation, if one is running - a cross, a look or the
        // partner's slot waits for the release like any other click here.
        bool lbHandledByTrade = mOTrade.TryPress(pbRightButton, lOMousePos, out UWDragSource lOTradeSource);

        if (lbHandledByTrade && lOTradeSource.Kind != UWDragSource.KindEnum.TradeClick)
            return;

        if (lbHandledByTrade || lOTradeSource.IsTrade)
        {
            fBeginPending(lOTradeSource, pbRightButton, lOMousePos);
            return;
        }

        // The classic panel: the container icon, the scroll arrows, the open container, the
        // backpack, the paperdoll (UWClassicPanelClick.SourceAt).
        UWDragSource lOHit = mOClick.SourceAt(lOMousePos, mbPendingIsUseClick);

        if (lOHit.Kind != UWDragSource.KindEnum.None)
            fBeginPending(lOHit, pbRightButton, lOMousePos);
    }

    /// <summary>The press found its source: it waits for the release or the movement threshold.</summary>
    private void fBeginPending(UWDragSource pOSource, bool pbRightButton, Vector2 pOMousePos)
    {
        mOSource = pOSource;
        mbUsingRightButton = pbRightButton;
        mOPendingStartPos = pOMousePos;
        meState = DragState.Pending;
    }

    private void fUpdatePending()
    {
        InputAction lOButtonAction = mbUsingRightButton ? mOControls.Player.Interact : mOControls.Player.CursorDrag;

        if (lOButtonAction.WasReleasedThisFrame())
        {
            // The left click that cuts a look text short in a conversation is used up - no
            // answer, no cross, no slot (per user on the original, 2026-09-18).
            if (!mbUsingRightButton && mOTrade.ConsumesLeftClick)
            {
                meState = DragState.Idle;
                return;
            }

            fReleasePending();
            return;
        }

        Vector2 lOMousePos = fPointerPosition();

        if (Vector2.Distance(mOPendingStartPos, lOMousePos) < ClickMoveThreshold)
            return; // Button still held, but no significant movement yet.

        meState = DragState.Idle;

        // No dragging possible from the container icon or the scroll arrows (no item
        // behind them).
        if (!mOSource.HoldsThing)
            return;

        // An item from an open container behaves like any other source when dragged
        // (backpack/equipment/world) - removed immediately and attached to the pointer,
        // no exception (confirmed by user). In a conversation a container stays where it is
        // (UWClassicTradeDrag.RefusesContainer).
        if (mOTrade.RefusesContainer(fPeekPendingItem(), mOSource, mOUWData))
            return;

        // A stack is not picked up immediately: the original first asks how many
        // to take.
        int liStackCount = fGetPendingStackCount();

        if (liStackCount > 1)
        {
            fBeginCountPrompt(liStackCount);
            return;
        }

        fTakeFromSource(1);
    }

    /// <summary>
    /// Button released before the movement threshold was exceeded: simple
    /// click instead of a drag. The item was never removed, so only the look reaction
    /// (right, ALWAYS - confirmed by user, 2026-08-27: "right click executes the
    /// look command", no exception for usable items) or, on the left, either
    /// the combat mode toggle on the active combat hand, opening a
    /// container item, closing it via its icon, OR - on a
    /// usable inventory item (see UWItemTargetRules.Classify) - the
    /// start of use mode (confirmed by user: left click starts it, not
    /// right click - an initial wrong assumption corrected here). Only for
    /// inventory sources (backpack/container/equipment), not for world objects.
    /// </summary>
    private void fReleasePending()
    {
        meState = DragState.Idle;

        if (mOSource.Kind == UWDragSource.KindEnum.TradeClick)
        {
            mOSource.Click();
        }
        // A click on a bought good takes it - a stack asks how many first.
        else if (mOSource.Kind == UWDragSource.KindEnum.NpcTrade)
        {
            int liBoughtCount = fGetPendingStackCount();

            if (liBoughtCount > 1)
                fBeginCountPrompt(liBoughtCount);
            else
            {
                fTakeFromSource(1);
                fLetGoOfButton();
            }
        }
        else if (mOSource.Kind == UWDragSource.KindEnum.ScrollArrow)
        {
            mOInventory.ScrollContainer(mOSource.Index);
        }
        else if (mOSource.Kind == UWDragSource.KindEnum.ContainerIcon)
        {
            mOInventory.CloseContainer();
        }
        // A click that does not carry the use is the look (see fIsUseClick: the right
        // button outside the use mode); a use click takes the path below.
        else if (!mbPendingIsUseClick)
        {
            // Original (per user, 2026-08-30): an EMPTY weapon hand also starts
            // combat mode on right click, unarmed. If a weapon is in it, however,
            // the right click looks at it - then combat mode only works via left click.
            if (!mOClick.TryToggleCombatFromEmptyHand(mOSource))
                mOClick.Look(fGetPendingSlotItem());
        }
        else
        {
            mOClick.Use(mOSource, fGetPendingSlotItem());
        }
    }

    /// <summary>How many pieces lie at the drag source. 1 if it is not a stack.</summary>
    private int fGetPendingStackCount()
    {
        return UWStackRules.CountAtLeastOne(fPeekPendingItem());
    }

    /// <summary>The world thing's picture while the source is the world - null once it is gone.</summary>
    private UWEntityInfo fWorldEntity()
    {
        return mOSource.Kind == UWDragSource.KindEnum.World ? mOSource.Entity as UWEntityInfo : null;
    }

    private UWObject fPeekPendingItem()
    {
        // A stack ON THE GROUND is also queried before it goes to the pointer - the
        // original asks for the quantity there just as in the inventory (per user,
        // 2026-09-07). Without this branch the world source stayed unknown, the quantity
        // therefore at one, and the whole stack silently came along.
        UWEntityInfo lOEntity = fWorldEntity();

        if (lOEntity != null)
            return lOEntity.ObjectData;

        return fGetPendingSlotItem();
    }

    /// <summary>
    /// Attaches the requested number of pieces to the pointer. If it is less than the whole
    /// stack, the rest stays behind - the object is split for that.
    /// </summary>
    private void fTakeFromSource(int piCount)
    {
        int liTotal = fGetPendingStackCount();
        UWEntityInfo lOEntity = fWorldEntity();

        // FROM THE WORLD: UWWorldPickup - the weight of the chosen count ("That is too heavy for
        // you to pick up.", UW.EXE seg024_24DC_BF4, block 1 no. 0x5F, 96 in ours; for a stack the
        // chosen quantity counts, per user 2026-09-13, as the original's AskHowMany cuts the
        // stack before EncumCheck), the theft, the moonstone and the removal. The reach was
        // measured at the press (UWClassicWorldGesture.fApply), before the count prompt, as player_3dget
        // asks InPickRange before AskHowMany.
        if (lOEntity != null)
        {
            if (mOInteraction == null || !UWWorldPickup.Take(mOInteraction, lOEntity, piCount,
                UWWorldPickup.Destination.Pointer, true))
            {
                fCancelDrag();
                return;
            }

            fBeginActualDrag();
            return;
        }

        UWConversationTrade lOTrade = mOSource.IsTrade ? mOTrade.Trade : null;

        // From either side of the trade the weight counts: "That is too heavy to take." and the
        // good stays there (UWPickupRules.CheckFromTrade, the chosen count weighed as
        // UseTradeSlot_ovr097_6E8 does for both sides; until 2026-10-15 ours weighed only the
        // partner's). Its message goes onto the parchment (UWClassicTradeDrag.ShowRefusal).
        if (mOSource.IsTrade)
        {
            UWPickupRules.Result lOCheck = UWPickupRules.CheckFromTrade(fPeekPendingItem(), piCount, mOInventory.Model);

            if (!lOCheck.CanTake)
            {
                mOTrade.ShowRefusal(lOCheck.MessageIndex, mOInteraction, mOUWData);
                fCancelDrag();
                return;
            }
        }

        // Part of a stack is only counted off, the stack stays where it is.
        if (piCount < liTotal && mOSource.TakePart(mOInventory.Model, lOTrade, piCount))
        {
            fBeginActualDrag();
            return;
        }

        mOSource.TakeWhole(mOInventory.Model, lOTrade);
        fBeginActualDrag();
    }

    /// <summary>The classic "Move how many?" prompt (UWCountPrompt on UWCountInput since cleanup
    /// step 5.13, 2026-10-22; until then its keys, pad and line lived here).</summary>
    private UWCountPrompt mOCountPrompt;

    private UWCountPrompt fCountPrompt()
    {
        if (mOCountPrompt == null)
            mOCountPrompt = new UWCountPrompt(mOInteraction);

        return mOCountPrompt;
    }

    /// <summary>The classic "Move how many?" prompt is running (the gamepad's pointer and letter
    /// grid stand aside, UWGamepadPointer, UWTextEntryState.IsTypingText).</summary>
    public bool IsAskingCount => meState == DragState.CountPrompt;

    private void fBeginCountPrompt(int piMaximum)
    {
        fCountPrompt().Begin(piMaximum);
        meState = DragState.CountPrompt;
    }

    /// <summary>Keyboard, mouse and pad during the prompt (UWCountPrompt). Returns true while the
    /// prompt is running - then the rest of the drag flow must not run - and in the frame it
    /// ended: the count taken goes onto the pointer, a cancel forgets the source.</summary>
    private bool fUpdateCountPrompt()
    {
        if (!IsAskingCount)
            return false;

        switch (mOCountPrompt.Update())
        {
            case UWCountPrompt.Outcome.Taken:
                // The item sticks to the pointer afterwards, it no longer depends on a held
                // button (see UWCountPrompt.fEnd).
                meState = DragState.Idle;
                fTakeFromSource(mOCountPrompt.TakenCount);
                fLetGoOfButton();
                break;

            case UWCountPrompt.Outcome.Cancelled:
                meState = DragState.Idle;
                mOSource = UWDragSource.None;
                break;
        }

        return true;
    }

    private void fBeginActualDrag()
    {
        meState = DragState.HeldDrag;
        mODragIcon.Hold(mOInventory.CursorItem);
    }

    /// <summary>The held button is let go: the thing lands where the pointer is (UWClassicDrop),
    /// or it sticks.</summary>
    private void fEndHeldDrag(Vector2 pOMousePos)
    {
        if (mODrop.TryPlaceAt(pOMousePos))
        {
            fEndDragAfterPlacement();
            return;
        }

        // An invalid drop does NOT cancel the drag (confirmed in the original) - the item
        // keeps "sticking" to the pointer until a later click (see Update()) hits a
        // valid target. Only the held button is released.
        fLetGoOfButton();
    }

    /// <summary>After a successful placement: if the target slot was occupied, the item
    /// previously there now hangs in CursorItem - the drag continues seamlessly with it (sticky,
    /// as with an invalid drop) instead of ending. If the target slot was empty, the drag ends
    /// normally.</summary>
    private void fEndDragAfterPlacement()
    {
        if (mOInventory.CursorItem == null)
        {
            fCancelDrag();
            return;
        }

        mOSource = UWDragSource.None;
        fLetGoOfButton();
        mODragIcon.Change(mOInventory.CursorItem);
    }

    /// <summary>The item at the currently remembered click source (backpack/equipment/
    /// open container/trade), still in its source slot - null for a world source (which
    /// already has its own description directly on UWEntityInfo, see
    /// UWClassicPanelClick.Look).</summary>
    private UWObject fGetPendingSlotItem()
    {
        return mOSource.Peek(mOInventory.Model, mOTrade.Trade);
    }

    /// <summary>For the modern bags (UWModernBags): the thing on the pointer dropped on the
    /// paperdoll's head - eaten by the same rule as the classic drop (UWClassicDrop.TryEatOnHead);
    /// false when it is nothing to eat (a helmet goes onto the head then).</summary>
    public bool TryConsumeCursorItemOnHead()
    {
        return mODrop.TryEatOnHead();
    }

    // ------------------------------------------------- For UWCarriedItemUse

    /// <summary>The game data this component reads (UWItemTargetRules.Classify needs it).</summary>
    public UWDataImport.DataImport GameData => mOUWData;

    /// <summary>Whether the inventory is there to use anything from.</summary>
    public bool HasInventory => mOInventory != null;

    /// <summary>The overview map of the current level, full screen.</summary>
    public void OpenMap()
    {
        mOClick.OpenMap();
    }

    /// <summary>What takes effect at once (UWItemUse.TryUseImmediately) with a host of its own
    /// (UWItemUseHost). pbConsumeAnywhere: used from the modern scheme, the piece leaves from
    /// wherever it lies; else from the classic click's source slot.</summary>
    public bool UseAtOnce(UWObject pOItem, bool pbConsumeAnywhere)
    {
        UWCharacter lOCharacter = UWScene.Character;
        UWItemUseHost lOHost = new UWItemUseHost(mOUseServices, pOItem,
            pbConsumeAnywhere ? UWItemUseHost.ConsumptionEnum.Anywhere : UWItemUseHost.ConsumptionEnum.FromSource, mOSource);

        return UWItemUse.TryUseImmediately(pOItem, mOUWData,
            lOCharacter != null ? lOCharacter.Vitals : null,
            mOInventory != null ? mOInventory.Model : null, lOHost);
    }

    /// <summary>The classic use mode for a carried piece: its icon on the pointer and "Use ...
    /// on what?".</summary>
    public void BeginUseMode(UWObject pOItem)
    {
        mOUseMode.Begin(pOItem);
    }

    /// <summary>The classic left click that used nothing: the combat toggle on the weapon hand
    /// (UWClassicPanelClick.ToggleCombatFromClick, on the click's source).</summary>
    public void ToggleCombatFromClick()
    {
        mOClick.ToggleCombatFromClick(mOSource);
    }

    /// <summary>For the modern bags: "Use &lt;thing&gt; on what?", as the classic use mode asks.</summary>
    public void ReportUsePrompt(UWObject pOItem)
    {
        mOUseMode.ReportPrompt(pOItem);
    }

    /// <summary>A host for a use that consumes nothing carried - the incense block burnt where it
    /// lies in the world (UWWorldUse).</summary>
    public IUWItemUseHost UseHostForWorldThing()
    {
        return new UWItemUseHost(mOUseServices, null, UWItemUseHost.ConsumptionEnum.Nothing, UWDragSource.None);
    }

    /// <summary>
    /// Making camp by key (F10) or by the modern panel's foot: in the bedroll if one is carried
    /// anywhere in the inventory, else on the bare ground (player_key_sleep: FindObj for the
    /// bedroll, then player_sleep(1) or (0)).
    /// </summary>
    public bool CampByKey()
    {
        bool lbBedroll = mOInventory != null && mOInventory.Model != null
            && mOInventory.Model.FindCarried(pOItem => pOItem.ID == UWSleep.BedrollObjectId) != null;

        return mOUseServices.Sleep(lbBedroll);
    }

    /// <summary>Use mode for a thing standing in the WORLD (the anvil) - the scheme's own if it
    /// has one (the modern bags, IUWUiScheme.TryBeginUseFromWorld), else UWClassicUseMode.BeginFromWorld.</summary>
    public void BeginUseModeFromWorld(UWObject pOItem)
    {
        if (!UWControlScheme.Ui.TryBeginUseFromWorld(pOItem))
            mOUseMode.BeginFromWorld(pOItem);
    }

    /// <summary>
    /// For Interaction (Original, right-click drag on a pickable thing in the 3D view): the
    /// thing goes onto the pointer - a stack asks for the count first, the carrying capacity
    /// and the theft are handled in fTakeFromSource as for any source. False when the inventory
    /// is busy (dragging, a pending click, the count prompt, use mode, something on the pointer)
    /// or takes no input - then the release falls through to Interaction.UseTarget.
    /// </summary>
    public bool TryBeginWorldDrag(UWEntityInfo pOTarget)
    {
        if (pOTarget == null || mOInventory == null || mOGameUi == null || State != DragState.Idle
            || mOInventory.CursorItem != null || mOGameUi.Map.IsMapVisible
            || (mOControlScheme != null && mOControlScheme.IsInventoryInputBlocked) || !UWControlScheme.Ui.Policy.WorldDragPicksUp)
            return false;

        mOSource = UWDragSource.World(pOTarget);
        mbPendingIsUseClick = false;
        mbUsingRightButton = true;
        mOPendingStartPos = fPointerPosition();

        int liStackCount = fGetPendingStackCount();

        if (liStackCount > 1)
            fBeginCountPrompt(liStackCount);
        else
            fTakeFromSource(1);

        return true;
    }

    /// <summary>For Interaction (use mode, right click in the view window): the held item onto
    /// what is under the pointer; use mode ends either way, as before.</summary>
    public void ApplyUseModeItemInWorld(Vector2 pOScreenPos)
    {
        if (mOInventory == null || mOInventory.UseModeItem == null || IsAskingCount
            || (mOInteraction != null && mOInteraction.IsSpellTargeting))
            return;

        mOUseMode.Apply(pOScreenPos, true);
    }

    /// <summary>The icon ends the frame on the pointer the frame is drawn with (UWDragIcon.Follow).</summary>
    private void LateUpdate()
    {
        if (mODragIcon != null)
            mODragIcon.Follow();
    }

    /// <summary>The position the pointer is drawn at (UWDragIcon.PointerPosition).</summary>
    private Vector2 fPointerPosition()
    {
        return UWDragIcon.PointerPosition(mOGameUi);
    }

    private void fCancelDrag()
    {
        // With a still held CursorItem, falls back to the known source slot
        // (or the first free backpack slot if the origin is unknown -
        // after a swap, see fEndDragAfterPlacement, for an item dragged from the world,
        // or for an item dragged from a container - all three deliberately fall back
        // only into the backpack instead of their old place) - a no-op if the item
        // was already placed successfully (CursorItem is then already null).
        // Dragged from the trade area: back there, as long as the slot is still free
        // (UWDragSource.Return).
        mOSource.Return(mOInventory.Model, mOSource.IsTrade ? mOTrade.Trade : null);

        meState = DragState.Idle;
        mOSource = UWDragSource.None;
        mODragIcon.Release();
    }
}
