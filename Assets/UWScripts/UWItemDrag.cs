using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UWDataImport;
using UWDataImport.UWData;

/// <summary>
/// Original controls, backpack/equipment slots: click+hold (left OR right, both
/// equivalent) on an occupied backpack slot or an equipped piece starts a
/// drag as soon as the mouse has moved a little afterwards (see mbIsPending/
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
/// released, see mbIsHeld) until a later click hits a valid target - the right button
/// anywhere, the left button only over the UI: in the view window the left button walks and
/// never throws the item (per user on the original, 2026-09-18).
///
/// If the button is released BEFORE the movement threshold was exceeded, it counts
/// as a simple click instead of a drag - each button with its own meaning: right click =
/// look (the item name is printed, like a world look target). Left click = open a
/// container, read, use the item immediately, or start "use" mode (the cursor carries the
/// item to apply it to a target), see fUpdatePending.
///
/// World pickup (original): right-click drag on a pickable object directly in the
/// 3D world (not backpack/paperdoll) works the same way - only with the right mouse button,
/// since the left one controls movement there (see UWPlayerMovement.fGetOriginalVelocity).
/// A simple right click (no drag) on such an object only shows its name,
/// just like with a backpack item - the actual pickup only happens on an
/// actual drag. Modern pickup (instant via right click, no drag) stays
/// unchanged in Interaction.cs.
///
/// Drop/throw (original): releasing (or a sticky right click) in the 3D view window itself
/// is also a valid target, see fTryDropOrThrowInWorld and UWPlayerThrow - the lower third of
/// the window drops in front of the player, above it the item is thrown as a projectile.
///
/// Manual hit testing (RectTransformUtility) instead of Unity UI events, same convention
/// as the rest of the project (see UWGameUI.fCursorMovement, UWModernBags) - there is
/// deliberately no EventSystem in the scene.
/// </summary>
public class UWItemDrag : MonoBehaviour, IUWItemUseHost
{
    private const float ClickMoveThreshold = UWWorldPointer.DragThreshold;

    private UWInventory mOInventory;
    private UWControlScheme mOControlScheme;
    private UWGameUI mOGameUi;
    private Interaction mOInteraction;
    private UWControls mOControls;
    private DataImport mOUWData;

    private GameObject mODragCanvas;
    private Image mODragIcon;
    private Texture2D mODragTexture;

    // Button pressed, source already determined, but the movement threshold has not yet
    // been exceeded - the actual drag (removal from the inventory, see
    // fBeginActualDrag) has not started yet.
    private bool mbIsPending;
    private Vector2 mOPendingStartPos;

    private bool mbIsDragging;
    private bool mbIsHeld;
    private bool mbUsingRightButton;

    // Source slot of the item currently dragged/targeted - during mbIsPending used for the
    // "look"/click feedback (the item is still there), during mbIsDragging only
    // for the case of a final cancel (see fCancelDrag/
    // UWInventory.CancelCursorItem). After a swap (see fEndDragAfterPlacement) the
    // origin of the NEW cursor item is unknown and all sources are set to null - a
    // cancel then falls back to the first free backpack slot.
    private UWArmorItemMap.BodySlot? mODragSourceSlot;
    private int? mODragSourceBackpackSlot;
    private int? mODragSourceContainerSlot;

    /// <summary>A slot of the player's trade area in a conversation (see
    /// UWConversationTrade) as the source of the drag.</summary>
    private int? mODragSourceTradeSlot;

    /// <summary>A bought good on the partner's side as the drag source - see
    /// UWConversationTrade.NpcBought. Dragged and asked "Move how many?" like the player's own
    /// trade slots (per user on the original, 2026-09-29).</summary>
    private int? mODragSourceNpcTradeSlot;

    private UWConversationScreen mOConversationScreen;

    /// <summary>The trade of the running conversation, otherwise null.</summary>
    private UWConversationTrade fGetTrade()
    {
        if (mOConversationScreen == null)
            mOConversationScreen = GetComponent<UWConversationScreen>();

        if (mOConversationScreen == null || !mOConversationScreen.IsOpen || mOConversationScreen.Session == null)
            return null;

        return mOConversationScreen.Session.Trade;
    }

    /// <summary>
    /// A press on the trade areas of the conversation. Returns true if the press is handled
    /// there - the action itself waits in mOPendingTradeAction for the release, as everything
    /// does during a conversation (per user on the original, 2026-09-18; until then the cross
    /// and the look acted on the press). Instead sets mODragSourceTradeSlot if a drag from the
    /// player's own area begins (then it continues like any other source).
    ///
    /// UseTradeSlot_ovr097_6E8 (BARTER.C): the slot picks up the item (a left drag), marks it (a
    /// left click) or describes it (right); the partner's items can only be marked and looked at
    /// until they are bought. The crosses beside the slots are only marks (see
    /// UWConversationScreen.GetPlayerTradeSlotAt).
    /// </summary>
    private bool fTryHandleTradeClick(bool pbRightButton, Vector2 pOMousePos)
    {
        UWConversationTrade lOTrade = fGetTrade();

        if (lOTrade == null)
            return false;

        int liSlot = mOConversationScreen.GetPlayerTradeSlotAt(pOMousePos);

        if (liSlot >= 0)
        {
            int liChosen = liSlot;

            if (lOTrade.PlayerItems[liSlot] == null)
                return true;

            if (pbRightButton)
            {
                mOPendingTradeAction = () => mOConversationScreen.Session.LookAtOwnTradeItem(lOTrade.PlayerItems[liChosen]);
                return true;
            }

            mODragSourceTradeSlot = liSlot;
            return false;
        }

        liSlot = mOConversationScreen.GetNpcTradeSlotAt(pOMousePos);

        if (liSlot < 0)
            return false;

        int liNpcSlot = liSlot;

        if (pbRightButton && lOTrade.NpcItems[liSlot] != null)
            mOPendingTradeAction = () => mOConversationScreen.Session.LookAtPartnerTradeItem(lOTrade.NpcItems[liNpcSlot]);
        else if (lOTrade.NpcGoodsBought && lOTrade.NpcBought[liSlot] && lOTrade.NpcItems[liSlot] != null)
        {
            // A bought good: a source like any other - dragged, or taken by a click.
            mODragSourceNpcTradeSlot = liSlot;
            return false;
        }
        else
            mOPendingTradeAction = () => lOTrade.ToggleNpcSelected(liNpcSlot);

        return true;
    }

    /// <summary>What a press on the trade areas does on its release - see fTryHandleTradeClick.</summary>
    private System.Action mOPendingTradeAction;
    private UWEntityInfo mODragSourceWorldEntity;
    private bool mbDragSourceIsContainerOpenIcon;

    /// <summary>The pending click carries the USE - decided once on the press by fIsUseClick;
    /// everything that reacts to a use click asks this, never the button.</summary>
    private bool mbPendingIsUseClick;
    private int? mODragSourceScrollDelta;

    /// <summary>For UWPlayerMovement: during an original-style drag, crossing
    /// the 3D view window must not also count as cursor movement.</summary>
    public bool IsDragging
    {
        get { return mbIsDragging; }
    }

    /// <summary>The drag with the button still held - only then does the left button belong to
    /// the drag. Once the item merely sticks to the pointer, the left button walks again (per
    /// user on the original, 2026-09-18).</summary>
    public bool IsHeldDrag
    {
        get { return mbIsDragging && mbIsHeld; }
    }

    private void Awake()
    {
        mOInventory = GetComponent<UWInventory>();

        if (mOInventory == null)
            mOInventory = gameObject.AddComponent<UWInventory>();

        mOControlScheme = GetComponentInParent<UWControlScheme>();
        mOGameUi = GetComponent<UWGameUI>();
        mOInteraction = GetComponent<Interaction>();
    }

    private void Start()
    {
        if (mOControlScheme != null)
            mOControls = mOControlScheme.Controls;

        UWLevelLoader lOLoader = UWScene.LevelLoader;

        if (lOLoader != null)
            mOUWData = lOLoader.UWDataImporter;

        fBuildDragCanvas();

        // Changing the object number along with the image lives here; the inventory only reports that
        // a light source has burned out.
        if (mOInventory != null)
            mOInventory.LightBurnedOut += fOnLightBurnedOut;
    }

    private void OnDestroy()
    {
        if (mOInventory != null)
            mOInventory.LightBurnedOut -= fOnLightBurnedOut;

        // The drag canvas lives at the root (fBuildDragCanvas) and goes with this component.
        if (mODragCanvas != null)
            Destroy(mODragCanvas);
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
        if (mOGameUi.IsMapVisible)
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
        // fTryResolveTargetSpellInInventory).
        //
        // ONLY IN THE CLASSIC SCHEME: its inventory is not on screen in the modern one, yet its
        // slots were still hit - a wand's Name Enchantment went onto an invisible backpack slot
        // under the modern bags and was gone (per user, 2026-10-04). The modern bags take it.
        bool lbClassicScheme = !UWControlScheme.IsModern;

        if (lbClassicScheme && mOInteraction != null && mOInteraction.IsSpellTargeting
            && !fTryResolveTargetSpellInInventory())
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

        bool lbIsOriginal = !UWControlScheme.IsModern;

        if (!lbIsOriginal)
        {
            if (mbIsDragging)
                fCancelDrag();
            else if (mbIsPending)
                mbIsPending = false; // Item was never removed - just forget it.

            if (mOInventory.UseModeItem != null)
                fEndUseMode();

            return;
        }

        if (mOInventory.UseModeItem != null)
        {
            fUpdateUseMode();
            return;
        }

        if (mbIsPending)
        {
            fUpdatePending();
            return;
        }

        // If something hangs on the pointer without a drag running, it becomes a sticky drag: this is how
        // an item that a conversation partner hands over (take_from_npc, as in the
        // original) gets to its place - and likewise whatever a cancelled drag with a full
        // backpack left on the pointer. Before, it was stuck there invisibly (per user,
        // 2026-09-11: "it sticks to the cursor").
        if (!mbIsDragging && mOInventory.CursorItem != null)
        {
            fBeginActualDrag();
            mbIsHeld = false;
            return;
        }

        if (!mbIsDragging)
        {
            if (UWMouseButtons.LeftPressed)
                fTryStartPending(false);
            else if (mOControls.Player.Interact.WasPressedThisFrame() && !UWPlayerMovement.ConsumesRightClick())
                fTryStartPending(true);

            return;
        }

        Vector2 lOMousePos = fPointerPosition();
        mODragIcon.rectTransform.position = lOMousePos;

        if (mbIsHeld)
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
            bool lbOverUi = !mOGameUi.IsScreenPositionInGameArea(lOMousePos)
                || (mOControlScheme != null && mOControlScheme.IsConversationOpen);

            if (mOControls.Player.Interact.WasPressedThisFrame()
                || (lbOverUi && UWMouseButtons.LeftPressed))
            {
                if (fTryPlaceAt(lOMousePos))
                    fEndDragAfterPlacement();
            }
        }
    }

    /// <summary>
    /// A pending target spell, discharged onto the inventory.
    ///
    /// THIS IS HOW THE ORIGINAL BEHAVES (per user, 2026-09-10): a click into the inventory - with
    /// any button - consumes the spell, even if it hits nothing valid. And the
    /// click still does what it otherwise does: whoever has the wand in a container and
    /// hits its close button with the left button loses the spell AND closes the
    /// container.
    ///
    /// That is exactly the return value: true means "the click may continue".
    ///
    /// A projectile spell does not get through here (IsItemTargetSpellPending) - for it
    /// the inventory stays silent as before.
    /// </summary>
    private bool fTryResolveTargetSpellInInventory()
    {
        if (!mOInteraction.IsItemTargetSpellPending)
            return false;

        if (!UWMouseButtons.LeftPressed
            && !mOControls.Player.Interact.WasPressedThisFrame())
            return false;

        Vector2 lOMousePos = fPointerPosition();

        // In the view window Interaction does the targeting itself - there the right button counts, and the
        // left one walks.
        if (mOGameUi.IsScreenPositionInGameArea(lOMousePos))
            return false;

        UWObject lOItem = fGetInventoryItemAt(lOMousePos);

        mOInteraction.ResolveTargetSpellOnItem(lOItem);

        // If the spell found a target, the click is consumed by it - otherwise it would
        // also look at or pick up the item right away.
        return lOItem == null;
    }

    /// <summary>
    /// Which item lies under the pointer - container window, backpack or
    /// paperdoll, in this order.
    ///
    /// The order is the same as in fTryStartPending and for the same reason: an
    /// open container lies exactly on top of the backpack.
    /// </summary>
    private UWObject fGetInventoryItemAt(Vector2 pOScreenPosition)
    {
        int liContainerSlot = mOGameUi.GetContainerSlotUnderMouse(pOScreenPosition);

        if (liContainerSlot >= 0)
            return mOGameUi.GetContainerItem(liContainerSlot);

        int liBackpackSlot = mOGameUi.GetBackpackSlotUnderMouse(pOScreenPosition);

        if (liBackpackSlot >= 0)
            return mOGameUi.GetBackpackItem(liBackpackSlot);

        UWArmorItemMap.BodySlot? leZone = mOGameUi.GetArmorHitZoneUnderMouse(pOScreenPosition);

        return leZone.HasValue && mOInventory != null
            ? mOInventory.GetEquipped(leZone.Value)
            : null;
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
            mOConversationScreen != null && mOConversationScreen.IsOpen);
    }

    /// <summary>Checks whether there is a pickable item under the mouse at all, and remembers
    /// its source - the actual drag only begins in fUpdatePending, once the
    /// movement threshold is exceeded.</summary>
    private void fTryStartPending(bool pbRightButton)
    {
        Vector2 lOMousePos = fPointerPosition();

        // Always reset all sources before exactly one of them (if any
        // is hit at all) is set anew.
        mODragSourceSlot = null;
        mODragSourceBackpackSlot = null;
        mODragSourceContainerSlot = null;
        mODragSourceWorldEntity = null;
        mbDragSourceIsContainerOpenIcon = false;
        mODragSourceScrollDelta = null;
        mODragSourceTradeSlot = null;
        mODragSourceNpcTradeSlot = null;
        mOPendingTradeAction = null;

        // Decided FIRST, before any of the sources below - the trade area asks it on the
        // release as well (per user, 2026-09-19: a left click there must mark, not look).
        mbPendingIsUseClick = fIsUseClick(pbRightButton);

        // The trade areas of the conversation, if one is running - a cross, a look or the
        // partner's slot waits for the release like any other click here.
        if (fTryHandleTradeClick(pbRightButton, lOMousePos))
        {
            if (mOPendingTradeAction == null)
                return;

            mbUsingRightButton = pbRightButton;
            mOPendingStartPos = lOMousePos;
            mbIsPending = true;
            return;
        }

        if (mODragSourceTradeSlot.HasValue || mODragSourceNpcTradeSlot.HasValue)
        {
            mbUsingRightButton = pbRightButton;
            mOPendingStartPos = lOMousePos;
            mbIsPending = true;
            return;
        }

        // Original: the container icon (above left of the container panel, only visible with
        // an open container) closes on left click - no dragging possible, so
        // only checked for the left button at all.
        if (mbPendingIsUseClick && mOGameUi.IsContainerOpenIconUnderMouse(lOMousePos))
        {
            mbDragSourceIsContainerOpenIcon = true;
            mbUsingRightButton = pbRightButton;
            mOPendingStartPos = lOMousePos;
            mbIsPending = true;
            return;
        }

        // Original: the scroll arrows scroll the container window by a whole row on left click
        // (confirmed by user) - no dragging possible either, left
        // button only. The UpArrow moves the entire content UP (confirmed by user) -
        // the current second row moves up to the first, the ones behind it (further back in the
        // container) become visible; so it increases the scroll offset rather than decreasing
        // it (the reverse for the DownArrow).
        if (mbPendingIsUseClick && mOGameUi.IsContainerScrollUpUnderMouse(lOMousePos))
        {
            mODragSourceScrollDelta = UWGameUI.ContainerColumns;
            mbUsingRightButton = pbRightButton;
            mOPendingStartPos = lOMousePos;
            mbIsPending = true;
            return;
        }

        if (mbPendingIsUseClick && mOGameUi.IsContainerScrollDownUnderMouse(lOMousePos))
        {
            mODragSourceScrollDelta = -UWGameUI.ContainerColumns;
            mbUsingRightButton = pbRightButton;
            mOPendingStartPos = lOMousePos;
            mbIsPending = true;
            return;
        }

        // If a container is open, its panel lies exactly on top of the backpack (see
        // UWGameUI.GetBackpackSlotUnderMouse, which then returns -1 itself) - so check this
        // first.
        int liContainerSlot = mOGameUi.GetContainerSlotUnderMouse(lOMousePos);

        if (liContainerSlot >= 0)
        {
            if (mOGameUi.GetContainerItem(liContainerSlot) == null)
                return;

            mODragSourceContainerSlot = liContainerSlot;
        }
        else
        {
            int liSlot = mOGameUi.GetBackpackSlotUnderMouse(lOMousePos);

            if (liSlot >= 0)
            {
                if (mOGameUi.GetBackpackItem(liSlot) == null)
                    return;

                mODragSourceBackpackSlot = liSlot;
            }
            else
            {
                // Nothing hit in the backpack - instead check whether an equipped
                // piece is being dragged off the body (unequipping). Fixed hit zones (see
                // UWGameUI.mArmorHitZones), not the texture-dependent armour images.
                UWArmorItemMap.BodySlot? leZone = mOGameUi.GetArmorHitZoneUnderMouse(lOMousePos);

                // The EMPTY weapon hand also counts as a click target - unarmed combat
                // starts there (per user, 2026-08-30). Without this exception an
                // empty body slot was never registered, and the click went nowhere.
                bool lbEmptyWeaponHand = leZone.HasValue && leZone.Value == mOInventory.MainHandSlot;

                if (leZone.HasValue && (lbEmptyWeaponHand || mOInventory.GetEquipped(leZone.Value) != null))
                {
                    mODragSourceSlot = leZone;
                }
                else
                {
                    // Neither backpack nor paper doll hit. The 3D view is Interaction's: it reads the
                    // right button once (UWWorldPointer) and calls TryBeginWorldDrag when a pickable
                    // thing is dragged - until 2026-09-18 a second raycast and a second click
                    // threshold for the same button lived here.
                    return;
                }
            }
        }

        mbUsingRightButton = pbRightButton;
        mOPendingStartPos = lOMousePos;
        mbIsPending = true;
    }

    private void fUpdatePending()
    {
        InputAction lOButtonAction = mbUsingRightButton ? mOControls.Player.Interact : mOControls.Player.CursorDrag;

        if (lOButtonAction.WasReleasedThisFrame())
        {
            // The left click that cuts a look text short in a conversation is used up - no
            // answer, no cross, no slot (per user on the original, 2026-09-18).
            if (!mbUsingRightButton && mOConversationScreen != null && mOConversationScreen.IsOpen
                && mOConversationScreen.ConsumesLeftClick)
            {
                mbIsPending = false;
                return;
            }

            // Button released before the movement threshold was exceeded: simple
            // click instead of a drag. The item was never removed, so only the look reaction
            // (right, ALWAYS - confirmed by user, 2026-08-27: "right click executes the
            // look command", no exception for usable items) or, on the left, either
            // the combat mode toggle on the active combat hand, opening a
            // container item, closing it via its icon, OR - on a
            // usable inventory item (see UWItemTargetRules.Classify) - the
            // start of use mode (confirmed by user: left click starts it, not
            // right click - an initial wrong assumption corrected here). Only for
            // inventory sources (backpack/container/equipment), not for world objects.
            if (mOPendingTradeAction != null)
            {
                mOPendingTradeAction();
            }
            // A click on a bought good takes it - a stack asks how many first.
            else if (mODragSourceNpcTradeSlot.HasValue)
            {
                mbIsPending = false;

                int liBoughtCount = fGetPendingStackCount();

                if (liBoughtCount > 1)
                    fBeginCountPrompt(liBoughtCount);
                else
                {
                    fTakeFromSource(1);
                    mbIsHeld = false;
                }

                return;
            }
            else if (mODragSourceScrollDelta.HasValue)
            {
                mOInventory.ScrollContainer(mODragSourceScrollDelta.Value);
            }
            else if (mbDragSourceIsContainerOpenIcon)
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
                if (!fTryToggleCombatFromEmptyHand())
                    fLookAtPendingItem();
            }
            else
            {
                UWObject lOPendingItem = fGetPendingSlotItem();
                UWConversationTrade lOPendingTrade = mODragSourceTradeSlot.HasValue ? fGetTrade() : null;

                // A plain left click on an item in the player's trade area marks it for the
                // deal, exactly like its cross (per user on the original, 2026-09-18).
                if (lOPendingTrade != null)
                    lOPendingTrade.TogglePlayerSelected(mODragSourceTradeSlot.Value);
                // The rune bag is a container, but does not behave like one:
                // instead of a container window the panel flips to the rune shelf
                // (per user, 2026-09-03). That is why this check comes BEFORE the
                // container check.
                else if (lOPendingItem != null && lOPendingItem.ID == UWObjectMechanics.RuneBagId)
                {
                    // Flipping to the rune shelf is a use, and in a conversation the left
                    // click uses nothing (per user, 2026-09-18) - it must not fall through to
                    // the container branch either, the bag is a container by category.
                    if (fGetTrade() == null)
                        mOGameUi.ToggleRunePanel();
                }
                else if (lOPendingItem != null && lOPendingItem.GetCategory() == UWObject.ObjectCategoryEnum.Containers)
                    mOInventory.TryOpenContainer(lOPendingItem);
                // In a conversation the left click does not use anything (per user, 2026-09-11);
                // opening containers and dragging still work - trading needs that. THE ONE
                // EXCEPTION is the combat mode from the weapon hand: the original flips it at
                // once, the Fight icon lights up, and the weapon is drawn only once the
                // conversation has returned to the game loop (per user on the original,
                // 2026-09-18). UWCharacter holds the weapon state machine while a conversation
                // is open, so the draw starts when the parchment is gone, as there.
                else if (fGetTrade() != null)
                {
                    fTryToggleCombatFromClick();
                }
                // The rest is the chain every scheme shares (UWCarriedItemUse): the map opens on
                // the LEFT click (the right click still shows its text), a book is read, what
                // works at once is used, a key or tool starts the use mode - and a click that used
                // nothing toggles the combat mode on the weapon hand.
                else
                    UWCarriedItemUse.Use(this, lOPendingItem, UWCarriedItemUse.Context.ClassicPanel);
            }

            mbIsPending = false;
            return;
        }

        Vector2 lOMousePos = fPointerPosition();

        if (Vector2.Distance(mOPendingStartPos, lOMousePos) < ClickMoveThreshold)
            return; // Button still held, but no significant movement yet.

        mbIsPending = false;

        // No dragging possible from the container icon or the scroll arrows (no item
        // behind them).
        if (mbDragSourceIsContainerOpenIcon || mODragSourceScrollDelta.HasValue || mOPendingTradeAction != null)
            return;

        // An item from an open container behaves like any other source when dragged
        // (backpack/equipment/world) - removed immediately and attached to the pointer,
        // no exception (confirmed by user).
        // IN A CONVERSATION A CONTAINER STAYS WHERE IT IS: "You cannot barter a container.
        // Instead, remove the contents you want to trade." for a moment on the parchment, the
        // drag never starts (per user on the original, 2026-09-18; the reference refuses the
        // pickup the same way, uimanager_inventory). Only from the inventory - a container that
        // already lies in the trade area can be taken back. THE RUNE BAG IS EXEMPT: UW.EXE
        // (ovr121_241 at ovr121_3A5, read 2026-10-01) refuses major class 2 minor 0 in the
        // conversation's mouse mode 4 only when the index is not 0xF - 0x8F is the rune bag,
        // which Dr. Owl wants to see (conversation 14, show_inv for 143); ours refused it too
        // (per user, 2026-10-01).
        UWObject lOPending = fPeekPendingItem();

        if (lOPending != null && !mODragSourceTradeSlot.HasValue && !mODragSourceNpcTradeSlot.HasValue
            && lOPending.GetCategory() == UWObject.ObjectCategoryEnum.Containers
            && lOPending.ID != UWObjectMechanics.RuneBagId
            && mOConversationScreen != null && mOConversationScreen.IsOpen && mOUWData != null)
        {
            mOConversationScreen.ShowLookText(UWText.General(mOUWData, CannotBarterContainerMessage));
            return;
        }

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

    /// <summary>String block 1: "You cannot barter a container.  Instead, remove the contents
    /// you want to trade."</summary>
    private const int CannotBarterContainerMessage = UWStringIds.CannotBarterContainerMessage;

    /// <summary>How many pieces lie at the drag source. 1 if it is not a stack.</summary>
    private int fGetPendingStackCount()
    {
        return UWStackRules.CountAtLeastOne(fPeekPendingItem());
    }

    private UWObject fPeekPendingItem()
    {
        // A stack ON THE GROUND is also queried before it goes to the pointer - the
        // original asks for the quantity there just as in the inventory (per user,
        // 2026-09-07). Without this branch the world source stayed unknown, the quantity
        // therefore at one, and the whole stack silently came along.
        if (mODragSourceWorldEntity != null)
            return mODragSourceWorldEntity.ObjectData;

        if (mODragSourceTradeSlot.HasValue)
        {
            UWConversationTrade lOTrade = fGetTrade();

            return lOTrade != null ? lOTrade.PlayerItems[mODragSourceTradeSlot.Value] : null;
        }

        if (mODragSourceNpcTradeSlot.HasValue)
        {
            UWConversationTrade lOTrade = fGetTrade();

            return lOTrade != null ? lOTrade.NpcItems[mODragSourceNpcTradeSlot.Value] : null;
        }

        if (mODragSourceContainerSlot.HasValue)
            return mOInventory.GetContainerItem(mODragSourceContainerSlot.Value);

        if (mODragSourceBackpackSlot.HasValue)
            return mOInventory.Backpack[mODragSourceBackpackSlot.Value];

        if (mODragSourceSlot.HasValue)
            return mOInventory.GetEquipped(mODragSourceSlot.Value);

        return null;
    }

    /// <summary>
    /// Attaches the requested number of pieces to the pointer. If it is less than the whole
    /// stack, the rest stays behind - the object is split for that.
    /// </summary>
    private void fTakeFromSource(int piCount)
    {
        UWObject lOSource = fPeekPendingItem();
        int liTotal = fGetPendingStackCount();

        // FROM THE WORLD: UWWorldPickup - the weight of the chosen count ("That is too heavy for
        // you to pick up.", UW.EXE seg024_24DC_BF4, block 1 no. 0x5F, 96 in ours; for a stack the
        // chosen quantity counts, per user 2026-09-13, as the original's AskHowMany cuts the
        // stack before EncumCheck), the theft, the moonstone and the removal. The reach was
        // measured at the press (Interaction.fApply), before the count prompt, as player_3dget
        // asks InPickRange before AskHowMany.
        if (mODragSourceWorldEntity != null)
        {
            if (mOInteraction == null || !UWWorldPickup.Take(mOInteraction, mODragSourceWorldEntity, piCount,
                UWWorldPickup.Destination.Pointer, true))
            {
                fCancelDrag();
                return;
            }

            fBeginActualDrag();
            return;
        }

        // From either side of the trade the weight counts: "That is too heavy to take." and the
        // good stays there (UWPickupRules.CheckFromTrade, the chosen count weighed as
        // UseTradeSlot_ovr097_6E8 does for both sides; until 2026-10-15 ours weighed only the
        // partner's).
        if (mODragSourceNpcTradeSlot.HasValue || mODragSourceTradeSlot.HasValue)
        {
            UWPickupRules.Result lOCheck = UWPickupRules.CheckFromTrade(lOSource, piCount, mOInventory.Model);

            if (!lOCheck.CanTake)
            {
                // ON THE PARCHMENT, where the player looks (B16 of the cleanup plan, 2026-10-12):
                // the original prints 0xFC with game_sprint while the barter is up
                // (UseTradeSlot_ovr097_6E8), where the player reads it; ours wrote it into the
                // message box, which the parchment covers, so the refusal was never seen. The
                // modern trade shows it there as well (UWModernBags.fTakeFromTrade), as the barter
                // refusal of a container above does.
                if (lOCheck.MessageIndex >= 0 && mOConversationScreen != null && mOConversationScreen.IsOpen
                    && mOUWData != null)
                    mOConversationScreen.ShowLookText(UWText.General(mOUWData, lOCheck.MessageIndex));
                else if (mOInteraction != null && lOCheck.MessageIndex >= 0)
                    mOInteraction.AddGeneralMessage(lOCheck.MessageIndex);

                fCancelDrag();
                return;
            }
        }

        // Part of a stack is only counted off, the stack stays where it is.
        if (piCount < liTotal && mOInventory.TakeFromStack(lOSource, piCount))
        {
            if (mODragSourceNpcTradeSlot.HasValue && fGetTrade() != null)
                fGetTrade().NpcSlotChanged(mODragSourceNpcTradeSlot.Value);
            else if (mODragSourceTradeSlot.HasValue && fGetTrade() != null)
                fGetTrade().PlayerSlotPartTaken(mODragSourceTradeSlot.Value);

            fBeginActualDrag();
            return;
        }

        if (mODragSourceTradeSlot.HasValue)
        {
            UWConversationTrade lOTrade = fGetTrade();

            if (lOTrade != null)
                mOInventory.BeginDragFromExternal(lOTrade.TakeFromPlayerSlot(mODragSourceTradeSlot.Value));
        }
        else if (mODragSourceNpcTradeSlot.HasValue)
        {
            UWConversationTrade lOTrade = fGetTrade();

            if (lOTrade != null)
                mOInventory.BeginDragFromExternal(lOTrade.TakeFromNpcSlot(mODragSourceNpcTradeSlot.Value));
        }
        else if (mODragSourceContainerSlot.HasValue)
            mOInventory.BeginDragFromContainer(mODragSourceContainerSlot.Value);
        else if (mODragSourceBackpackSlot.HasValue)
            mOInventory.BeginDragFromBackpack(mODragSourceBackpackSlot.Value);
        else if (mODragSourceSlot.HasValue)
            mOInventory.BeginDragFromEquip(mODragSourceSlot.Value);

        fBeginActualDrag();
    }

    /// <summary>
    /// The quantity prompt, as in the original.
    ///
    /// When a drag starts on a stack, "Move how many? 1" appears with a blinking
    /// square after it. Enter accepts the shown number, typing replaces it, a
    /// left click takes one immediately, a right click takes all (per user, 2026-09-01).
    ///
    /// Whether the drag was started with left or right does not matter.
    /// </summary>
    private bool mbAskingCount;

    /// <summary>The classic "Move how many?" prompt is running (the gamepad's pointer and letter
    /// grid stand aside, UWGamepadPointer, UWGameUI.IsTypingText).</summary>
    public bool IsAskingCount => mbAskingCount;

    /// <summary>The gamepad's d-pad on the count: when the held direction steps next, and since when.</summary>
    private float mfPadCountNext;

    private float mfPadCountSince;

    private int miPadCountDirection;

    private int miAskMaximum;

    private string msAskInput = string.Empty;

    private void fBeginCountPrompt(int piMaximum)
    {
        mbFirstDigit = true;
        mbAskingCount = true;
        miAskMaximum = piMaximum;
        msAskInput = "1";

        // During the prompt the pointer is hidden (per user, 2026-09-01).
        Cursor.visible = false;

        fRefreshCountPrompt();
    }

    private void fRefreshCountPrompt()
    {
        if (mOInteraction == null)
            return;

        // Without the cursor - UWGameUI appends it to the last line while the prompt is running.
        mOInteraction.SetPromptMessage("Move how many? " + msAskInput);
    }

    /// <summary>
    /// Ends the prompt and picks up the quantity.
    ///
    /// AFTERWARDS THE ITEM STICKS TO THE POINTER, it no longer depends on a held button.
    /// Otherwise releasing the button just used to answer would already count as
    /// placing: a right click for "all" picked up the stack and threw it away again
    /// in the same move (per user, 2026-09-07).
    ///
    /// When answering via the keyboard there would be the same problem from the other side:
    /// if the button is released during the prompt, that release falls into a frame
    /// in which the drag flow does not run at all - the held drag would then wait for a
    /// release that never comes.
    /// </summary>
    private void fEndCountPrompt(int piCount)
    {
        mbAskingCount = false;

        if (mOInteraction != null)
        {
            mOInteraction.SetPromptMessage("Move how many? " + piCount);
            mOInteraction.EndPromptMessage();
        }

        fTakeFromSource(Mathf.Clamp(piCount, 1, miAskMaximum));

        mbIsHeld = false;
    }

    /// <summary>
    /// Cancel without picking up. On Esc the original writes a dash as the chosen number,
    /// on Enter with an empty field, however, nothing at all - the line simply stays
    /// (per user, 2026-09-01).
    /// </summary>
    private void fCancelCountPrompt(bool pbShowDash)
    {
        mbAskingCount = false;

        if (mOInteraction != null)
        {
            mOInteraction.SetPromptMessage("Move how many? " + (pbShowDash ? "-" : string.Empty));
            mOInteraction.EndPromptMessage();
        }

        mODragSourceSlot = null;
        mODragSourceBackpackSlot = null;
        mODragSourceContainerSlot = null;
        mODragSourceWorldEntity = null;
        mODragSourceTradeSlot = null;
        mODragSourceNpcTradeSlot = null;
        mOPendingTradeAction = null;

        Cursor.visible = true;
    }

    /// <summary>Keyboard and mouse during the prompt. Returns true while the prompt is running -
    /// then the rest of the drag flow must not run.</summary>
    private bool fUpdateCountPrompt()
    {
        if (!mbAskingCount)
            return false;

        fRefreshCountPrompt();

        Mouse lOMouse = Mouse.current;

        if (lOMouse != null && UWMouseButtons.LeftPressed)
        {
            fEndCountPrompt(1);
            return true;
        }

        if (lOMouse != null && UWMouseButtons.RightPressed)
        {
            fEndCountPrompt(miAskMaximum);
            return true;
        }

        if (fUpdateCountPromptPad())
            return true;

        Keyboard lOKeyboard = Keyboard.current;

        if (lOKeyboard == null)
            return true;

        if (lOKeyboard.enterKey.wasPressedThisFrame || lOKeyboard.numpadEnterKey.wasPressedThisFrame)
        {
            int liTyped;

            // Enter with an empty field cancels, but leaves no dash - unlike
            // Esc (per user, 2026-09-01).
            if (!int.TryParse(msAskInput, out liTyped))
                fCancelCountPrompt(false);
            else
                fEndCountPrompt(liTyped);

            return true;
        }

        if (lOKeyboard.escapeKey.wasPressedThisFrame)
        {
            fCancelCountPrompt(true);
            return true;
        }

        // The preset 1 disappears on the FIRST key press, whichever key. If it was
        // not a digit, the field stays empty and the cursor keeps blinking (per user,
        // 2026-09-01). This is also why the first digit replaces the 1 instead of
        // appending to it - it is the same rule, not two.
        if (mbFirstDigit && lOKeyboard.anyKey.wasPressedThisFrame)
        {
            msAskInput = string.Empty;
            mbFirstDigit = false;
        }

        if (lOKeyboard.backspaceKey.wasPressedThisFrame && msAskInput.Length > 0)
            msAskInput = msAskInput.Substring(0, msAskInput.Length - 1);

        for (int liDigit = 0; liDigit <= 9; liDigit++)
        {
            if (fWasDigitPressed(lOKeyboard, liDigit))
                msAskInput += liDigit;
        }

        return true;
    }

    private bool mbFirstDigit = true;

    /// <summary>
    /// THE GAMEPAD ON THE COUNT (per user, 2026-10-07: no letter grid, the d-pad turns the number):
    /// up and down step it, round from the maximum to 1 and back, held faster and after two
    /// seconds by ten; A takes that many, X all, B cancels. True when the prompt has ended.
    /// </summary>
    private bool fUpdateCountPromptPad()
    {
        Gamepad lOPad = Gamepad.current;

        if (lOPad == null)
            return false;

        if (lOPad.buttonSouth.wasPressedThisFrame)
        {
            fEndCountPrompt(int.TryParse(msAskInput, out int liCount) && liCount > 0 ? liCount : 1);
            return true;
        }

        if (lOPad.buttonWest.wasPressedThisFrame)
        {
            fEndCountPrompt(miAskMaximum);
            return true;
        }

        if (lOPad.buttonEast.wasPressedThisFrame)
        {
            fCancelCountPrompt(true);
            return true;
        }

        int liDirection = (lOPad.dpad.up.isPressed ? 1 : 0) - (lOPad.dpad.down.isPressed ? 1 : 0);

        if (liDirection == 0)
        {
            miPadCountDirection = 0;
            return false;
        }

        float lfNow = Time.unscaledTime;

        if (liDirection != miPadCountDirection)
        {
            miPadCountDirection = liDirection;
            mfPadCountSince = lfNow;
            mfPadCountNext = lfNow + 0.35f;
        }
        else if (lfNow < mfPadCountNext)
        {
            return false;
        }
        else
        {
            mfPadCountNext = lfNow + 0.07f;
        }

        int liStep = lfNow - mfPadCountSince > 2f ? 10 : 1;
        int liValue = int.TryParse(msAskInput, out int liNow) ? liNow : 1;

        liValue += liDirection * liStep;

        // Round from the end to the start - a step by ten stops at the end first.
        if (liValue > miAskMaximum)
            liValue = liStep > 1 && liNow < miAskMaximum ? miAskMaximum : 1;
        else if (liValue < 1)
            liValue = liStep > 1 && liNow > 1 ? 1 : miAskMaximum;

        msAskInput = liValue.ToString();
        mbFirstDigit = false;
        fRefreshCountPrompt();

        return false;
    }

    private static bool fWasDigitPressed(Keyboard pOKeyboard, int piDigit)
    {
        switch (piDigit)
        {
            case 0: return pOKeyboard.digit0Key.wasPressedThisFrame || pOKeyboard.numpad0Key.wasPressedThisFrame;
            case 1: return pOKeyboard.digit1Key.wasPressedThisFrame || pOKeyboard.numpad1Key.wasPressedThisFrame;
            case 2: return pOKeyboard.digit2Key.wasPressedThisFrame || pOKeyboard.numpad2Key.wasPressedThisFrame;
            case 3: return pOKeyboard.digit3Key.wasPressedThisFrame || pOKeyboard.numpad3Key.wasPressedThisFrame;
            case 4: return pOKeyboard.digit4Key.wasPressedThisFrame || pOKeyboard.numpad4Key.wasPressedThisFrame;
            case 5: return pOKeyboard.digit5Key.wasPressedThisFrame || pOKeyboard.numpad5Key.wasPressedThisFrame;
            case 6: return pOKeyboard.digit6Key.wasPressedThisFrame || pOKeyboard.numpad6Key.wasPressedThisFrame;
            case 7: return pOKeyboard.digit7Key.wasPressedThisFrame || pOKeyboard.numpad7Key.wasPressedThisFrame;
            case 8: return pOKeyboard.digit8Key.wasPressedThisFrame || pOKeyboard.numpad8Key.wasPressedThisFrame;
            default: return pOKeyboard.digit9Key.wasPressedThisFrame || pOKeyboard.numpad9Key.wasPressedThisFrame;
        }
    }

    private void fBeginActualDrag()
    {
        mbIsDragging = true;
        mbIsHeld = true;
        fShowDragIcon(mOInventory.CursorItem);

        // The normal pointer (movement direction arrows, see UWGameUI.Update) is
        // replaced by the icon while dragging instead of showing both at once -
        // UWGameUI only sets the cursor when Cursor.visible is true, and leaves it alone when
        // false.
        Cursor.visible = false;
    }

    private void fEndHeldDrag(Vector2 pOMousePos)
    {
        if (fTryPlaceAt(pOMousePos))
        {
            fEndDragAfterPlacement();
            return;
        }

        // An invalid drop does NOT cancel the drag (confirmed in the original) - the item
        // keeps "sticking" to the pointer until a later click (see Update()) hits a
        // valid target. Only the held button is released.
        mbIsHeld = false;
    }

    /// <summary>
    /// Places the item from the pointer and extinguishes a burning light source in the process
    /// if it does not end up in a hand slot.
    ///
    /// Only there may one burn (per user, checked in the original, 2026-09-05) - putting it into the
    /// backpack or a container puts it out.
    ///
    /// Returns false if fTryPlaceAtCore found no valid action; the item then stays stuck to
    /// the pointer (see fEndHeldDrag).
    /// </summary>
    private bool fTryPlaceAt(Vector2 pOMousePos)
    {
        if (!fTryPlaceAtCore(pOMousePos))
            return false;

        // PUTTING IT OUT IS THE MODEL'S JOB (UWInventoryModel.ExtinguishOutsideHand), which does it
        // in every place that takes an item. Here it used to happen for EVERY successful
        // placement, and that also caught the trade area of a conversation - where the original
        // lets a light keep burning, even so far that it lands burning on the floor when the
        // conversation ends with it in a slot (per user on the original, 2026-09-18).
        mOInventory.ApplyCarriedLight();

        return true;
    }

    /// <summary>Tries to place CursorItem at the given screen position (also
    /// as a swap if the target is occupied). Returns true only for an actually
    /// valid action - a recognised target that does not fit the item (e.g.
    /// non-armour on an armour zone) counts as invalid and returns false, the
    /// item then stays stuck to the pointer (see fEndHeldDrag).</summary>
    private bool fTryPlaceAtCore(Vector2 pOMousePos)
    {
        // The player's own trade area in a conversation takes the item from the pointer; if one is
        // already there, that one goes to the pointer (swap, as with the backpack). The partner's
        // area accepts nothing.
        UWConversationTrade lOTrade = fGetTrade();

        if (lOTrade != null)
        {
            int liSlot = mOConversationScreen.GetPlayerTradeSlotAt(pOMousePos);

            if (liSlot >= 0)
            {
                // The slot's own rule (UWConversationTrade.DropOnPlayerSlot, CombineToSlot_ovr097_D30):
                // a like pile takes the count (per user, 2026-09-29: the original stacks there),
                // anything else swaps, a container there refuses - then the thing stays on the
                // pointer, as a drop that found no place.
                UWObject lOHeld = mOInventory.TakeCursorItem();
                UWObject lOBack = lOTrade.DropOnPlayerSlot(liSlot, lOHeld);

                if (lOBack != null)
                    mOInventory.BeginDragFromExternal(lOBack);

                return lOBack != lOHeld;
            }

            if (mOConversationScreen.GetNpcTradeSlotAt(pOMousePos) >= 0)
                return false;
        }

        // During a held/sticky drag the container icon is a drop target like
        // any other - puts the item into the first free backpack slot (confirmed by
        // user). Without an item on the pointer a simple click on it closes the
        // container instead (see fUpdatePending) - the two cases are mutually
        // exclusive, since fTryPlaceAt is only ever called with a set CursorItem.
        if (mOGameUi.IsContainerOpenIconUnderMouse(pOMousePos))
        {
            // ONE LEVEL UP, as closing goes: from a container opened inside another the item goes
            // into that one, not into the pack (per user, 2026-09-30). It then takes it like a
            // drop onto the closed container, with the same refusals.
            UWObject lOParent = mOInventory.ParentContainer;

            if (lOParent != null)
            {
                if (mOInventory.DropCursorItemIntoContainerItem(lOParent))
                    return true;

                fReportContainerRejection();
                return false;
            }

            if (mOInventory.DropCursorItemInFirstFreeBackpackSlot())
                return true;

            // Original wording (confirmed by user): general message 259, ItemFitsSlot's 0x102.
            mOInteraction?.AddGeneralMessage(UWStringIds.NoPlaceToPutMessage);
            return false;
        }

        // The scroll arrows also work during a held/sticky drag -
        // they only scroll the view and are not a drop target themselves (the item stays stuck
        // to the pointer).
        if (mOGameUi.IsContainerScrollUpUnderMouse(pOMousePos))
        {
            mOInventory.ScrollContainer(UWGameUI.ContainerColumns);
            return false;
        }

        if (mOGameUi.IsContainerScrollDownUnderMouse(pOMousePos))
        {
            mOInventory.ScrollContainer(-UWGameUI.ContainerColumns);
            return false;
        }

        UWArmorItemMap.BodySlot? leHitZone = mOGameUi.GetArmorHitZoneUnderMouse(pOMousePos);

        if (leHitZone.HasValue)
        {
            if (leHitZone.Value == UWArmorItemMap.BodySlot.Helmet && fTryConsumeOnHead())
                return true;

            if (mOInventory.DropCursorItemInEquip(leHitZone.Value))
                return true;

            // The rune bag held there refuses anything but a rune stone with its message.
            fReportContainerRejection();
            return false;
        }

        // Exactly on top of the backpack (see fTryStartPending) - only one of the two
        // returns a hit at all, depending on GetBackpackSlotUnderMouse/OpenContainer.
        int liContainerSlot = mOGameUi.GetContainerSlotUnderMouse(pOMousePos);

        if (liContainerSlot >= 0)
        {
            if (mOInventory.DropCursorItemInContainer(liContainerSlot))
                return true;

            fReportContainerRejection();
            return false;
        }

        int liBackpackSlot = mOGameUi.GetBackpackSlotUnderMouse(pOMousePos);

        if (liBackpackSlot >= 0)
        {
            // If the drop attempt hits a backpack slot that itself contains a container,
            // the item goes into its contents instead of displacing the container
            // (confirmed by user) - the container does not have to be opened for this.
            UWObject lOSlotItem = mOGameUi.GetBackpackItem(liBackpackSlot);

            if (lOSlotItem != null && lOSlotItem.GetCategory() == UWObject.ObjectCategoryEnum.Containers)
            {
                if (mOInventory.DropCursorItemIntoContainerItem(lOSlotItem))
                    return true;

                fReportContainerRejection();
                return false;
            }

            return mOInventory.DropCursorItemInBackpack(liBackpackSlot);
        }

        // In a conversation the parchment is there, not the world - nothing falls behind it.
        if (mOControlScheme != null && mOControlScheme.IsConversationOpen)
            return false;

        if (mOGameUi.IsScreenPositionInGameArea(pOMousePos))
            return fTryDropOrThrowInWorld(pOMousePos);

        return false;
    }

    /// <summary>Original: release/sticky right click in the 3D view window drops or throws the
    /// item - see UWPlayerThrow, which follows DropOrThrowByPlayer_seg025_355 of UW.EXE. A failed
    /// drop does NOT cancel the drag (see fTryPlaceAt), the item stays at the pointer.
    /// UWItemDrag sits on Camera.main, so transform is the player camera itself.</summary>
    private bool fTryDropOrThrowInWorld(Vector2 pOMousePos)
    {
        if (mOLevelLoader == null)
            mOLevelLoader = UWScene.LevelLoader;

        return UWPlayerThrow.TryDropOrThrow(pOMousePos, transform, mOInventory, mOLevelLoader, mOGameUi,
            mOInteraction);
    }

    private UWLevelLoader mOLevelLoader;

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

        mODragSourceSlot = null;
        mODragSourceBackpackSlot = null;
        mODragSourceContainerSlot = null;
        mODragSourceWorldEntity = null;
        mODragSourceTradeSlot = null;
        mODragSourceNpcTradeSlot = null;
        mOPendingTradeAction = null;
        mbIsHeld = false;
        fShowDragIcon(mOInventory.CursorItem);
    }

    /// <summary>The item at the currently remembered click source (backpack/equipment/
    /// open container), still in its source slot - null for a world source (which
    /// already has its own description directly on UWEntityInfo, see
    /// fLookAtPendingItem).</summary>
    private UWObject fGetPendingSlotItem()
    {
        if (mODragSourceTradeSlot.HasValue)
        {
            UWConversationTrade lOTrade = fGetTrade();

            return lOTrade != null ? lOTrade.PlayerItems[mODragSourceTradeSlot.Value] : null;
        }

        if (mODragSourceNpcTradeSlot.HasValue)
        {
            UWConversationTrade lOTrade = fGetTrade();

            return lOTrade != null ? lOTrade.NpcItems[mODragSourceNpcTradeSlot.Value] : null;
        }

        if (mODragSourceContainerSlot.HasValue)
            return mOGameUi.GetContainerItem(mODragSourceContainerSlot.Value);
        if (mODragSourceBackpackSlot.HasValue)
            return mOGameUi.GetBackpackItem(mODragSourceBackpackSlot.Value);
        if (mODragSourceSlot.HasValue)
            return mOInventory.GetEquipped(mODragSourceSlot.Value);
        return null;
    }

    /// <summary>Opens the overview map of the current level.</summary>
    private void fOpenMap()
    {
        if (mOGameUi == null)
            return;

        // The level the character is currently standing on.
        mOGameUi.ShowMap(mOGameUi.CurrentMapLevelIndex);
    }

    /// <summary>Like a world look target, but for a backpack/equipment/container/
    /// world item that is still in its source slot (a simple click never
    /// removed it). The message comes from Interaction.DescribeInventoryItem, which reads
    /// the name with +1 on the object ID, since Strings.Blocks[4] is indexed shifted by
    /// one relative to the object ID (see UWArmorItemMap - verified at tile (24,10) on level 1).
    /// For a world object whose object data yields no description, the finished one is taken from
    /// UWEntityInfo.Description instead (the same one Interaction.cs uses for normal
    /// world look targets) - the raw original string,
    /// UWObjectDescriptionFormatter builds the "You see ..." message from it (see there for
    /// the "_"/"&amp;" structure).</summary>
    private void fLookAtPendingItem()
    {
        if (mOInteraction == null)
            return;

        UWObject lOItem = fGetPendingSlotItem();

        if (mOUWData == null || lOItem == null)
            return;

        // IN A CONVERSATION the look goes onto the parchment: the description replaces the
        // answers for two seconds, as for an item in the trade area (per user on the original,
        // 2026-09-18). The message box behind the parchment is not visible there.
        if (fGetTrade() != null || (mOConversationScreen != null && mOConversationScreen.IsOpen && mOConversationScreen.Session != null))
        {
            mOConversationScreen.Session.LookAt(lOItem);
            return;
        }

        // A text scroll is also read on right click (per user, 2026-09-03).
        // The look message is not lost - it is the first line of what is
        // read. Scrolls without text (picture parchment, map) fall through and are
        // only described, as before.
        if (mOInteraction.TryReadBook(lOItem, true))
            return;

        try
        {
            mOInteraction.AddItemLookMessages(lOItem);
        }
        catch
        {
            // Like fGetDescription in UWObjectSpawner: a missing string block entry is not an
            // error, simply no message.
        }
    }

    /// <summary>Original: simple left click (no drag) on the hand slot that is currently
    /// the active combat hand (UWInventory.MainHandSlot) - toggles combat mode, exactly
    /// like Interaction.ToggleCombatMode, but only if the hand is empty or holds
    /// a weapon. Every other left click (other slot, backpack, world) has
    /// no effect here.</summary>
    private void fTryToggleCombatFromClick()
    {
        if (mOInteraction == null || !mODragSourceSlot.HasValue || mODragSourceSlot.Value != mOInventory.MainHandSlot)
            return;

        UWObject lOItem = mOInventory.GetEquipped(mODragSourceSlot.Value);

        // An empty hand also starts combat mode - unarmed then (per user,
        // 2026-08-30). Before, a click on the empty hand had no effect, so unarmed combat
        // was not reachable at all.
        if (lOItem != null && lOItem.GetCategory() != UWObject.ObjectCategoryEnum.WeaponsAndMissiles)
            return;

        mOInteraction.ToggleCombatMode();
    }

    /// <summary>Right click on the empty weapon hand: unarmed combat mode. Returns false
    /// if something is there or another slot was meant - then it is looked at.</summary>
    private bool fTryToggleCombatFromEmptyHand()
    {
        if (mOInteraction == null || !mODragSourceSlot.HasValue
            || mODragSourceSlot.Value != mOInventory.MainHandSlot)
            return false;

        if (mOInventory.GetEquipped(mODragSourceSlot.Value) != null)
            return false;

        mOInteraction.ToggleCombatMode();

        return true;
    }

    /// <summary>
    /// WHAT LANDS ON THE HEAD IS EATEN. In the original the routine that decides about a drop
    /// on a paperdoll slot calls Eat for slot 0 (MaybeContainerLogic_ovr121_B4B). The user
    /// checked it on 2026-09-21: leeches give "You manage to finish eating the leeches.
    /// Barely.", a potion "You quaff the potion in one gulp.", a wine bottle "You are unable to
    /// open the wine bottle."
    ///
    /// IT IS NOT THE ORDINARY USE PATH, which was the first reading and was wrong: USED, the
    /// leeches bite and suck the poison out, and the user confirmed that this effect stays away
    /// when they go on the head - they are simply eaten. So the drop takes UWItemUse.TryEat.
    ///
    /// A HELMET still goes onto the head: TryEat says no to it, and then the drop continues as
    /// before into the slot.
    /// </summary>
    private bool fTryConsumeOnHead()
    {
        UWObject lOItem = mOInventory != null ? mOInventory.CursorItem : null;

        if (lOItem == null)
            return false;

        UWCharacter lOCharacter = UWScene.Character;

        mbConsumeFromCursor = true;
        mOUsedItem = lOItem;

        try
        {
            if (!UWItemUse.TryEat(lOItem, mOUWData,
                lOCharacter != null ? lOCharacter.Vitals : null, this))
                return false;
        }
        finally
        {
            mOUsedItem = null;
            mbConsumeFromCursor = false;
        }

        // Whatever is left of it stays on the cursor - a stack that gave up one piece, a
        // bottle that is now empty. Only what the rules consumed is gone already.
        return true;
    }

    /// <summary>For the modern bags (UWModernBags): the thing on the pointer dropped on the
    /// paperdoll's head - eaten by the same rule as the classic drop (fTryConsumeOnHead); false
    /// when it is nothing to eat (a helmet goes onto the head then).</summary>
    public bool TryConsumeCursorItemOnHead()
    {
        return fTryConsumeOnHead();
    }

    /// <summary>Set while something is used straight off the CURSOR - see fTryConsumeOnHead
    /// and ConsumeUsedItem.</summary>
    private bool mbConsumeFromCursor;

    /// <summary>Everything that takes effect immediately on click instead of waiting for a target
    /// - the rules are in UWItemUse (P3 of the engine separation, 2026-09-18); this class is
    /// its host and knows where the used piece came from.</summary>
    private bool fTryUseItemImmediately(UWObject pOItem)
    {
        UWCharacter lOCharacter = UWScene.Character;

        mOUsedItem = pOItem;

        try
        {
            return UWItemUse.TryUseImmediately(pOItem, mOUWData,
                lOCharacter != null ? lOCharacter.Vitals : null,
                mOInventory != null ? mOInventory.Model : null, this);
        }
        finally
        {
            mOUsedItem = null;
        }
    }

    /// <summary>The item currently being used, for ConsumeUsedItem.</summary>
    private UWObject mOUsedItem;

    /// <summary>Set while a carried piece is used from the modern scheme (UWCarriedItemUse): no
    /// click source is remembered, ConsumeUsedItem takes it out of the inventory wherever it lies.</summary>
    private bool mbConsumeAnywhere;

    // ------------------------------------------------- For UWCarriedItemUse

    /// <summary>The game data this component reads (UWItemTargetRules.Classify needs it).</summary>
    public UWDataImport.DataImport GameData => mOUWData;

    /// <summary>Whether the inventory is there to use anything from.</summary>
    public bool HasInventory => mOInventory != null;

    /// <summary>The overview map of the current level, full screen.</summary>
    public void OpenMap()
    {
        fOpenMap();
    }

    /// <summary>What takes effect at once (UWItemUse.TryUseImmediately) with this component as
    /// its host. pbConsumeAnywhere: used from the modern scheme, the piece leaves from wherever it
    /// lies; else from the classic click's source slot.</summary>
    public bool UseAtOnce(UWObject pOItem, bool pbConsumeAnywhere)
    {
        mbConsumeAnywhere = pbConsumeAnywhere;

        try
        {
            return fTryUseItemImmediately(pOItem);
        }
        finally
        {
            mbConsumeAnywhere = false;
        }
    }

    /// <summary>The classic use mode for a carried piece: its icon on the pointer and "Use ...
    /// on what?".</summary>
    public void BeginUseMode(UWObject pOItem)
    {
        fBeginUseMode(pOItem);
    }

    /// <summary>The classic left click that used nothing: the combat toggle on the weapon hand
    /// (fTryToggleCombatFromClick).</summary>
    public void ToggleCombatFromClick()
    {
        fTryToggleCombatFromClick();
    }

    /// <summary>For the modern bags: "Use &lt;thing&gt; on what?", as the classic use mode asks.</summary>
    public void ReportUsePrompt(UWObject pOItem)
    {
        fReportUsePrompt(pOItem);
    }

    // ------------------------------------------------- IUWItemUseHost

    public void ConsumeUsedItem()
    {
        if (mOUsedItem == null)
            return;

        // Used from the modern scheme (UWCarriedItemUse): no click source is remembered, the piece
        // leaves from wherever it lies.
        if (mbConsumeAnywhere)
        {
            if (UWStackRules.TryTakeOne(mOUsedItem))
            {
                mOInventory.NotifyChanged();

                return;
            }

            mOInventory.RemoveItem(mOUsedItem);

            return;
        }

        // Used off the cursor (the drop on the head): the piece hangs on the pointer and no
        // longer in the slot it came from, so taking it out of that slot would delete whatever
        // moved there in the meantime.
        if (mbConsumeFromCursor)
        {
            if (UWStackRules.TryTakeOne(mOUsedItem))
                return;

            mOInventory.TakeCursorItem();

            return;
        }

        fConsumeItem(mOUsedItem);
    }

    public void AddGeneralMessage(int piIndex)
    {
        if (mOInteraction != null)
            mOInteraction.AddGeneralMessage(piIndex);
    }

    public void AddMessage(string psMessage)
    {
        if (mOInteraction != null)
            mOInteraction.AddMessage(psMessage);
    }

    public void CursePlayer(int piDice)
    {
        UWCharacter lOCharacter = UWScene.Character;

        if (lOCharacter != null)
            lOCharacter.ApplyCurse(piDice);
    }

    public int PlayerDamageTypeProof => UWScene.Character != null ? UWScene.Character.DamageTypeProof : 0;

    public void FlashWindow(int piPaletteColour, float pfSeconds)
    {
        UWGameUI lOUi = UWScene.GameUi;

        if (lOUi != null)
            lOUi.FlashWindowColour(piPaletteColour, pfSeconds);
    }

    public void PlayCutscene(int piCutscene)
    {
        UWIntroPlayer lOCutscene = UWScene.IntroPlayer;

        if (lOCutscene != null)
            lOCutscene.PlayCutscene(piCutscene);
    }

    /// <summary>Do NOT fire immediately: on the click the pointer is over the inventory, not in
    /// the view window. A projectile therefore waits for the target click as with rune casting -
    /// exactly as the original does it (per user, 2026-09-05).</summary>
    public bool CastSpellFromObject(int piSpellIndex)
    {
        return mOGameUi != null && mOGameUi.CastSpellFromObject(piSpellIndex, false);
    }

    public bool CastSpellByClass(int piMajorClass, int piMinorClass)
    {
        return mOGameUi != null && mOGameUi.CastSpellByClass(piMajorClass, piMinorClass);
    }

    public bool IsWandCoolingDown => Time.time < mfNextWandUseTime;

    public void StartWandCooldown()
    {
        mfNextWandUseTime = Time.time + WandCooldownSeconds;
    }

    public void PlayMagicItemRefused()
    {
        UWSoundEffects.PlayAtAvatar(UWSoundEffects.MagicItemRefused);
    }

    /// <summary>IUWItemUseHost: the bedroll used - a night in it.</summary>
    public bool TrySleep()
    {
        return fTrySleep(true);
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

        return fTrySleep(lbBedroll);
    }

    public void SleepPassedOut()
    {
        if (mOInteraction == null)
            return;

        // PASSED OUT, not gone to bed: the original asks nothing then - not the ground, not
        // the enemies - and lets the surface do its work instead (see UWSleepRules.Sleep).
        UWSleep.Sleep(mOUWData, UWScene.Character, mOInteraction,
            UWScene.LevelLoader, true);
    }

    public bool TryPlantSeed()
    {
        return UWSilverTree.TryPlant(UWScene.LevelLoader, transform, mOInteraction);
    }

    public bool IsInstrument(int piObjectId)
    {
        return UWInstrumentPlayer.IsInstrument(piObjectId);
    }

    public bool PlayInstrument(UWObject pOItem)
    {
        return UWInstrumentPlayer.Begin(mOInteraction != null ? mOInteraction.gameObject : gameObject, pOItem);
    }

    public bool TryFish()
    {
        return UWFishing.TryFish(UWScene.LevelLoader, transform, mOInventory, mOInteraction);
    }

    /// <summary>
    /// Removes the consumed piece from the game - for a stack only one of them.
    ///
    /// Where it came from is stored in the same fields fPeekPendingItem fetched it from -
    /// backpack, open container or paperdoll (a world source never gets here: TryBeginWorldDrag
    /// takes the thing at once and never leaves a pending click). The order is the same as
    /// there, so that both refer to the same piece.
    /// </summary>
    private void fConsumeItem(UWObject pOItem)
    {
        // ONLY DECREMENT A REAL STACK. On an enchanted piece the same field carries the
        // enchantment id from 512 on - counting it down would silently change its spell
        // (UWStackRules).
        if (UWStackRules.TryTakeOne(pOItem))
            return;

        if (mODragSourceContainerSlot.HasValue)
        {
            mOInventory.RemoveFromContainer(mODragSourceContainerSlot.Value);

            return;
        }

        if (mODragSourceBackpackSlot.HasValue)
        {
            mOInventory.RemoveFromBackpack(mODragSourceBackpackSlot.Value);

            return;
        }

        if (mODragSourceSlot.HasValue)
            mOInventory.RemoveFromEquip(mODragSourceSlot.Value);
    }

    /// <summary>Says why a container refused the item - the sentences are
    /// UWItemMessages.ContainerRejection, shared with the modern bags.</summary>
    private void fReportContainerRejection()
    {
        if (mOInteraction == null || mOInventory == null)
            return;

        string lsMessage = UWItemMessages.ContainerRejection(mOInventory.LastContainerRejection,
            mOInventory.LastContainerRejectionContainer, mOUWData);

        if (lsMessage != null)
            mOInteraction.AddMessage(lsMessage);
    }

    /// <summary>Goes to sleep - see UWSleep. The bedroll stays in the
    /// backpack, it is not consumed.</summary>
    private bool fTrySleep(bool pbBedroll)
    {
        if (mOInteraction == null)
            return false;

        UWSleep.Sleep(mOUWData, UWScene.Character, mOInteraction,
            UWScene.LevelLoader, false, pbBedroll);

        return true;
    }

    /// <summary>For this long a wand cannot be used again after a use.
    /// It applies GLOBALLY and not per wand: after a use a
    /// second wand is also briefly dead. A RUNE spell is exempt and works immediately - it
    /// goes through UWGameUI.fCastSelectedRunes and never passes here (all per
    /// user, checked in the original, 2026-09-05).
    ///
    /// THE LENGTH IS THE ORIGINAL'S since 2026-09-24: CastSpellFromObject_seg040_1F04 sets its
    /// timer to the game clock plus 0x2FD, and the clock runs at 256 units a second
    /// (UWPlayerTick.PitTicksPerSecond), so just under three seconds; ours was an estimated two.</summary>
    private const float WandCooldownSeconds = WandCooldownClockUnits / (float)UWDataImport.UWData.UWPlayerTick.PitTicksPerSecond;

    private const int WandCooldownClockUnits = 0x2FD;

    private float mfNextWandUseTime;

    /// <summary>Use mode for a thing standing in the WORLD (the anvil): its image hangs on the
    /// cursor as for an inventory item (per user from the original, 2026-09-17: "using the
    /// anvil turns the cursor into the anvil"). The caller writes the prompt itself.</summary>
    public void BeginUseModeFromWorld(UWObject pOItem)
    {
        // The modern scheme has its own use mode in the bags (UWModernBags.BeginUse).
        if (UWControlScheme.IsModern
            && UWModernBags.Instance != null)
        {
            UWModernBags.Instance.BeginUse(pOItem, true);
            return;
        }

        mOInventory.EnterUseMode(pOItem);
        fShowDragIcon(pOItem);
        Cursor.visible = false;

        // The same prompt as from the pack - "Use <thing> on what?" - so the wording has one
        // owner (until 2026-09-19 the anvil said "on?" from Interaction, which appears nowhere
        // in the game image, see fReportUsePrompt).
        fReportUsePrompt(pOItem);
    }

    /// <summary>Starts use mode: the item stays in its place (unlike
    /// a drag), only its icon hangs on the cursor (same look as when dragging,
    /// fShowDragIcon).</summary>
    private void fBeginUseMode(UWObject pOItem)
    {
        mOInventory.EnterUseMode(pOItem);
        fShowDragIcon(pOItem);
        Cursor.visible = false;

        fReportUsePrompt(pOItem);
    }

    /// <summary>
    /// The prompt with which an item in use mode asks for its target: "Use &lt;thing&gt; on
    /// what?", for every item that asks.
    ///
    /// ONE SENTENCE, NOT IN STRINGS.PAK: UseObjectWithPrompt_seg040_5BA, the original's only
    /// routine for it, assembles "Use " + the item's name (or "UNNAMED") + " on what?" from the
    /// pieces at 0x5E6B9 of UW.EXE (found after the user reported the wording, 2026-09-10; the
    /// reference ends it with "on?", which appears nowhere in the game image). Its callers are
    /// the Key of Infinity, KeyLockpickUsage for the lockpick and the keys, the spike, skulls
    /// and bones, the anvil, the pole, the rock hammer, Tybal's orb and the oil flask.
    ///
    /// THE KEY, THE LOCKPICK AND THE SPIKE TOO (2026-09-27, per user with a screenshot of the
    /// original: "Use key on what?" and "Use lockpick on what?"; the spike goes through the same
    /// routine, not seen). Until then ours printed three sentences of the string block for them
    /// - "What lock would you like to try to pick?", "Use key on..." and "Please select door to
    /// spike..." (block 1, our 9, 8 and 131), the reference's choice, which UW.EXE never prints.
    ///
    /// NOT FOR WANDS AND SCROLLS: they are not among the routine's callers, and the original
    /// does not ask there either (answered 2026-09-27).
    /// </summary>
    private void fReportUsePrompt(UWObject pOItem)
    {
        if (mOInteraction == null)
            return;

        mOInteraction.AddMessage("Use " + UWText.ObjectName(mOUWData, pOItem.ID, false, false, UWItemMessages.UnnamedItem) + " on what?");
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
        if (pOTarget == null || mOInventory == null || mOGameUi == null || mbIsDragging || mbIsPending || mbAskingCount
            || mOInventory.UseModeItem != null || mOInventory.CursorItem != null || mOGameUi.IsMapVisible
            || (mOControlScheme != null && mOControlScheme.IsInventoryInputBlocked) || UWControlScheme.IsModern)
            return false;

        mODragSourceSlot = null;
        mODragSourceBackpackSlot = null;
        mODragSourceContainerSlot = null;
        mbDragSourceIsContainerOpenIcon = false;
        mODragSourceScrollDelta = null;
        mODragSourceTradeSlot = null;
        mODragSourceNpcTradeSlot = null;
        mOPendingTradeAction = null;
        mbPendingIsUseClick = false;
        mODragSourceWorldEntity = pOTarget;
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
        if (mOInventory == null || mOInventory.UseModeItem == null || mbAskingCount
            || (mOInteraction != null && mOInteraction.IsSpellTargeting))
            return;

        fTryApplyUseModeItem(pOScreenPos, true);
    }

    /// <summary>
    /// THE ICON FOLLOWS THE POINTER AFTER EVERYONE ELSE HAS MOVED IT (2026-10-06, per user: with a
    /// thing on the pointer and a turn under way, it drifted ahead of the pointer in the turn's
    /// direction). During a cursor movement UWGameUI.Update warps the pointer back onto the view
    /// window's edge every frame; Update here may run before that warp and so put the icon where
    /// the mouse had been pushed to, beyond the edge. LateUpdate runs after every Update, so the
    /// icon ends the frame on the pointer the frame is drawn with.
    /// </summary>
    private void LateUpdate()
    {
        if (mODragIcon == null || !mODragIcon.enabled || Mouse.current == null)
            return;

        mODragIcon.rectTransform.position = fPointerPosition();
    }

    /// <summary>The position the pointer is drawn at: UWGameUI's clamped one during a cursor
    /// movement (after WarpCursorPosition the Input System keeps reporting the pushed-out
    /// position), else the mouse's.</summary>
    private Vector2 fPointerPosition()
    {
        Vector2 lOMouse = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;

        if (mOGameUi != null && !UWControlScheme.IsModern
            && UWScene.PlayerMovement != null && UWScene.PlayerMovement.IsCursorMovementInProgress)
            return mOGameUi.PointerPosition;

        return lOMouse;
    }

    /// <summary>Original (confirmed by user, 2026-08-27): right click applies the item
    /// everywhere, left click only outside the 3D view window (there left still
    /// controls movement). Every application - whether it hits a valid target or
    /// not - ends use mode afterwards (confirmed by user: "after a
    /// use, use mode is always cancelled").</summary>
    private void fUpdateUseMode()
    {
        Vector2 lOMousePos = fPointerPosition();
        mODragIcon.rectTransform.position = lOMousePos;

        // THE ITEM IS THE POINTER (UseThing: force_mouse_cursor with the item, UnderworldExhumed
        // USEITEMS.C), so the system pointer stays hidden the whole time - not only at the start:
        // UWControlScheme shows it again in the classic scheme whenever a hold changes (per user,
        // 2026-10-13: the item hung on the pointer and the normal pointer showed as well).
        if (Cursor.visible && !mbAskingCount && (mOInteraction == null || !mOInteraction.IsAskingYesNo))
            Cursor.visible = false;

        // In a conversation there is no view window, only the parchment in front of it.
        bool lbInGameArea = mOGameUi.IsScreenPositionInGameArea(lOMousePos)
            && !(mOControlScheme != null && mOControlScheme.IsConversationOpen);

        // In the view window Interaction reads the right button (fUpdateUseModeInWorld) and calls
        // ApplyUseModeItemInWorld; here only the clicks on the inventory, with either button.
        bool lbTriggered = !lbInGameArea
            && (mOControls.Player.Interact.WasPressedThisFrame() || UWMouseButtons.LeftPressed);

        // A CLICK ON THE RUNES IS SWALLOWED: try_cast and mous_in_rune return in another input
        // mode, so the original keeps the use mode and casts nothing (per user, 2026-10-13: with a
        // key in use, a click into the hollow cast the spell in ours - the use mode ended first).
        if (lbTriggered && mOGameUi.Runes != null && mOGameUi.Runes.IsPointerOverRunes(lOMousePos))
            return;

        if (lbTriggered)
            fTryApplyUseModeItem(lOMousePos, lbInGameArea);
    }

    /// <summary>
    /// The use mode's click: the held item onto a carried thing or onto what is under the
    /// pointer in the view window - which item takes which target and what it does there is
    /// UWItemTargetRules, the carrying out UWItemTargetUse (cleanup step 2.4, 2026-10-12).
    /// Every application - whether it hits a valid target or not - ends use mode afterwards
    /// (confirmed by user: "after a use, use mode is always cancelled").
    ///
    /// THE ANVIL AND THE OIL FLASK GO ONTO A CARRIED THING: a click in the backpack, the open
    /// container or on the paperdoll says what is repaired or filled; a click into the view
    /// does nothing. The use mode ends FIRST there: it shows the pointer again, and the repair
    /// question then hides it and remembers it as visible - the other way round the pointer
    /// stayed hidden after the answer.
    ///
    /// NO additional look message for the hit target (confirmed by user,
    /// 2026-08-27 - a first attempt showed it additionally, which turned out to be duplicate/wrong:
    /// Interaction.cs already showed one through its own, independent
    /// right-click handling, see the UseModeItem lock there).
    /// </summary>
    private void fTryApplyUseModeItem(Vector2 pOMousePos, bool pbInGameArea)
    {
        UWObject lOItem = mOInventory.UseModeItem;

        if (lOItem != null && UWItemTargetRules.TakesCarriedTarget(lOItem.ID))
        {
            UWObject lOTarget = !pbInGameArea && mOInteraction != null ? fGetInventoryItemAt(pOMousePos) : null;

            fEndUseMode();
            UWItemTargetUse.Apply(mOInteraction, lOItem, lOTarget, null);

            return;
        }

        if (lOItem != null && pbInGameArea)
            UWItemTargetUse.Apply(mOInteraction, lOItem, null, pOMousePos);

        // A WORLD TOOL ON A CARRIED THING answers with its own refusal (mous_in_inv calls the
        // same second half, 2026-10-15; until then the click only ended the mode); the pole stays
        // in the use mode afterwards. A click on no thing ends the mode silently, as pick_inv's 0.
        if (lOItem != null && !pbInGameArea && mOInteraction != null)
        {
            UWObject lOCarried = fGetInventoryItemAt(pOMousePos);

            if (lOCarried != null && UWItemTargetUse.Apply(mOInteraction, lOItem, lOCarried, null))
                return;
        }

        fEndUseMode();
    }

    private void fEndUseMode()
    {
        mOInventory.CancelUseMode();
        mODragIcon.enabled = false;
        Cursor.visible = true;
    }

    private void fCancelDrag()
    {
        // With a still held CursorItem, falls back to the known source slot
        // (or the first free backpack slot if the origin is unknown -
        // after a swap, see fEndDragAfterPlacement, for an item dragged from the world,
        // or for an item dragged from a container - all three deliberately fall back
        // only into the backpack instead of their old place) - a no-op if the item
        // was already placed successfully (CursorItem is then already null).
        // Dragged from the trade area: back there, as long as the slot is still free.
        UWConversationTrade lOTrade = mODragSourceTradeSlot.HasValue ? fGetTrade() : null;

        if (lOTrade != null && mOInventory.CursorItem != null
            && lOTrade.PlayerItems[mODragSourceTradeSlot.Value] == null)
            lOTrade.PutInPlayerSlot(mODragSourceTradeSlot.Value, mOInventory.TakeCursorItem());

        // Taken from the partner's side: back onto its place there.
        UWConversationTrade lONpcTrade = mODragSourceNpcTradeSlot.HasValue ? fGetTrade() : null;

        if (lONpcTrade != null && mOInventory.CursorItem != null)
            lONpcTrade.ReturnToNpcSlot(mODragSourceNpcTradeSlot.Value, mOInventory.TakeCursorItem());

        mOInventory.CancelCursorItem(mODragSourceBackpackSlot, mODragSourceSlot);

        mbIsDragging = false;
        mbIsHeld = false;
        mODragSourceSlot = null;
        mODragSourceBackpackSlot = null;
        mODragSourceContainerSlot = null;
        mODragSourceWorldEntity = null;
        mODragSourceTradeSlot = null;
        mODragSourceNpcTradeSlot = null;
        mOPendingTradeAction = null;
        mODragIcon.enabled = false;
        Cursor.visible = true;
    }

    private void fShowDragIcon(UWObject pOItem)
    {
        UWTexture lOSource = pOItem?.Icon ?? pOItem?.Texture;

        if (lOSource == null)
        {
            mODragIcon.enabled = false;
            return;
        }

        if (mODragTexture != null)
            Destroy(mODragTexture);

        mODragTexture = UWIconTextureBuilder.Build(lOSource, FilterMode.Point);

        UWIconPalette.Apply(mODragIcon);

        mODragIcon.sprite = Sprite.Create(mODragTexture, new Rect(0f, 0f, mODragTexture.width, mODragTexture.height), new Vector2(0.5f, 0.5f), 1f);

        // AT THE POINTER AT ONCE: the position used to follow only in the next update, so for
        // one frame the icon stood where the last drag or use had ended (per user, 2026-09-26).
        if (Mouse.current != null)
            mODragIcon.rectTransform.position = fPointerPosition();

        mODragIcon.enabled = true;
        mODragIcon.rectTransform.localScale = new Vector3(UWGameUI.HorizontalPixelFactor, 1f, 1f);
    }

    /// <summary>
    /// Separate overlay canvas, same CanvasScaler as UWGameUI (ScaleWithScreenSize,
    /// 320x200 reference resolution) instead of fixed screen pixels - otherwise the icon was tiny
    /// compared to the rest of the UI (which is scaled up to the actual
    /// resolution via the same scaler). RectTransform.position = Mouse.current.position
    /// still stays correct: the scaler only affects how sizes given in reference units
    /// are converted to pixels, not the world-position-to-pixel
    /// mapping of a screen space overlay canvas.
    /// </summary>
    private void fBuildDragCanvas()
    {
        mODragCanvas = new GameObject("Item Drag Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));

        // AT THE ROOT, NOT UNDER THE CAMERA (2026-10-06, per user with a screenshot: with a thing on
        // the pointer every turn of the view - pointer or keys - put the thing beside the pointer
        // in the turn's direction, the faster the turn the farther). This component sits on the
        // camera; a canvas under it is turned with the player in the same frame, before Unity
        // drives it back to its screen pose, and a world position set from the mouse in that
        // moment is converted under the turned pose and stays wrong once the pose is restored.
        // An overlay canvas at the root is never moved by anyone; OnDestroy takes it down.
        mODragCanvas.transform.SetParent(null, false);

        Canvas lOCanvas = mODragCanvas.GetComponent<Canvas>();
        lOCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        lOCanvas.sortingOrder = 1000;

        CanvasScaler lOScaler = mODragCanvas.GetComponent<CanvasScaler>();
        lOScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        lOScaler.referenceResolution = new Vector2(320f, 200f);
        lOScaler.matchWidthOrHeight = 1f;

        // The same fitting as the frame, so the icon keeps its size in a narrow window (UWUiFit).
        mODragCanvas.AddComponent<UWFitCanvas>();

        GameObject lOIcon = new GameObject("DragIcon", typeof(RectTransform), typeof(Image));
        lOIcon.transform.SetParent(mODragCanvas.transform, false);

        // Must match UWGameUI.backpackSlotSize - in the original the icon is
        // exactly as large when dragging as in the backpack, no separate scale.
        const float DragIconSize = 14f;

        RectTransform lOIconRect = (RectTransform)lOIcon.transform;
        lOIconRect.sizeDelta = new Vector2(DragIconSize, DragIconSize);
        lOIconRect.pivot = new Vector2(0.5f, 0.5f);

        mODragIcon = lOIcon.GetComponent<Image>();
        mODragIcon.raycastTarget = false;
        mODragIcon.enabled = false;
    }
}
