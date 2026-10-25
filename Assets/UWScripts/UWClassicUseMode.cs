using System;
using UnityEngine;
using UWDataImport;
using UWDataImport.UWData;

/// <summary>
/// THE CLASSIC USE MODE (cleanup step 5.15, 2026-10-22: out of UWItemDrag): a carried thing - or
/// one standing in the world, the anvil - hangs on the pointer as its icon (UWDragIcon) and waits
/// for the click that says what it is used on. The thing in use is the inventory model's
/// UseModeItem, which Interaction reads as well; this class starts and ends the mode, asks
/// "Use ... on what?" and takes the click on the inventory - in the view window Interaction reads
/// the right button and comes back through UWItemDrag.ApplyUseModeItemInWorld.
///
/// Original (confirmed by user, 2026-08-27): right click applies the item everywhere, left click
/// only outside the 3D view window (there left still controls movement). Every application -
/// whether it hits a valid target or not - ends use mode afterwards (confirmed by user: "after a
/// use, use mode is always cancelled").
/// </summary>
public sealed class UWClassicUseMode
{
    private readonly UWInventory mOInventory;

    private readonly Interaction mOInteraction;

    private readonly UWGameUI mOGameUi;

    private readonly UWControlScheme mOControlScheme;

    private readonly UWDragIcon mOIcon;

    private readonly DataImport mOUWData;

    /// <summary>The count prompt is running (UWItemDrag.IsAskingCount) - its own hiding of the
    /// pointer is not undone then.</summary>
    private readonly Func<bool> mFIsAskingCount;

    public UWClassicUseMode(UWInventory pOInventory, Interaction pOInteraction, UWGameUI pOGameUi,
        UWControlScheme pOControlScheme, UWDragIcon pOIcon, DataImport pOData, Func<bool> pFIsAskingCount)
    {
        mOInventory = pOInventory;
        mOInteraction = pOInteraction;
        mOGameUi = pOGameUi;
        mOControlScheme = pOControlScheme;
        mOIcon = pOIcon;
        mOUWData = pOData;
        mFIsAskingCount = pFIsAskingCount;
    }

    /// <summary>A thing is in use (the model's UseModeItem).</summary>
    public bool IsActive => mOInventory != null && mOInventory.UseModeItem != null;

    /// <summary>Starts use mode: the item stays in its place (unlike
    /// a drag), only its icon hangs on the cursor (same look as when dragging,
    /// UWDragIcon).</summary>
    public void Begin(UWObject pOItem)
    {
        mOInventory.EnterUseMode(pOItem);
        mOIcon.Hold(pOItem);

        ReportPrompt(pOItem);
    }

    /// <summary>Use mode for a thing standing in the WORLD (the anvil): its image hangs on the
    /// cursor as for an inventory item (per user from the original, 2026-09-17: "using the
    /// anvil turns the cursor into the anvil"). The caller writes the prompt itself. The modern
    /// scheme has its own use mode in the bags (UWModernBags.BeginUse), which UWItemDrag asks for
    /// first (IUWUiScheme.TryBeginUseFromWorld since cleanup 5.19).</summary>
    public void BeginFromWorld(UWObject pOItem)
    {
        mOInventory.EnterUseMode(pOItem);
        mOIcon.Hold(pOItem);

        // The same prompt as from the pack - "Use <thing> on what?" - so the wording has one
        // owner (until 2026-09-19 the anvil said "on?" from Interaction, which appears nowhere
        // in the game image, see ReportPrompt).
        ReportPrompt(pOItem);
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
    public void ReportPrompt(UWObject pOItem)
    {
        if (mOInteraction == null)
            return;

        mOInteraction.AddMessage(UWText.F("prompt.use_on_what", UWText.ObjectName(mOUWData, pOItem.ID, false, false, UWItemMessages.UnnamedItem)));
    }

    /// <summary>Once a frame while a thing is in use (UWItemDrag.Update): the icon on the pointer,
    /// and the clicks on the inventory, with either button.</summary>
    public void Update(UWControls pOControls)
    {
        Vector2 lOMousePos = UWDragIcon.PointerPosition(mOGameUi);
        mOIcon.MoveTo(lOMousePos);

        // THE ITEM IS THE POINTER (UseThing: force_mouse_cursor with the item, UnderworldExhumed
        // USEITEMS.C), so the system pointer stays hidden the whole time - not only at the start:
        // UWControlScheme shows it again in the classic scheme whenever a hold changes (per user,
        // 2026-10-13: the item hung on the pointer and the normal pointer showed as well).
        mOIcon.KeepSystemPointerHidden(!mFIsAskingCount() && (mOInteraction == null || !mOInteraction.IsAskingYesNo));

        // In a conversation there is no view window, only the parchment in front of it.
        bool lbInGameArea = mOGameUi.ClassicScreen.IsScreenPositionInGameArea(lOMousePos)
            && !(mOControlScheme != null && mOControlScheme.IsConversationOpen);

        // In the view window Interaction reads the right button (fUpdateUseModeInWorld) and calls
        // ApplyUseModeItemInWorld; here only the clicks on the inventory, with either button.
        bool lbTriggered = !lbInGameArea
            && (pOControls.Player.Interact.WasPressedThisFrame() || UWMouseButtons.LeftPressed);

        // A CLICK ON THE RUNES IS SWALLOWED: try_cast and mous_in_rune return in another input
        // mode, so the original keeps the use mode and casts nothing (per user, 2026-10-13: with a
        // key in use, a click into the hollow cast the spell in ours - the use mode ended first).
        if (lbTriggered && mOGameUi.Runes != null && mOGameUi.Runes.IsPointerOverRunes(lOMousePos))
            return;

        if (lbTriggered)
            Apply(lOMousePos, lbInGameArea);
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
    public void Apply(Vector2 pOMousePos, bool pbInGameArea)
    {
        UWObject lOItem = mOInventory.UseModeItem;

        if (lOItem != null && UWItemTargetRules.TakesCarriedTarget(lOItem.ID))
        {
            UWObject lOTarget = !pbInGameArea && mOInteraction != null ? CarriedItemAt(mOGameUi, mOInventory, pOMousePos) : null;

            End();
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
            UWObject lOCarried = CarriedItemAt(mOGameUi, mOInventory, pOMousePos);

            if (lOCarried != null && UWItemTargetUse.Apply(mOInteraction, lOItem, lOCarried, null))
                return;
        }

        End();
    }

    /// <summary>Ends the mode: the thing stays where it is, its icon leaves the pointer and the
    /// system pointer comes back.</summary>
    public void End()
    {
        mOInventory.CancelUseMode();
        mOIcon.Release();
    }

    /// <summary>
    /// Which item lies under the pointer - container window, backpack or
    /// paperdoll, in this order.
    ///
    /// The order is the same as in UWItemDrag.fTryStartPending and for the same reason: an
    /// open container lies exactly on top of the backpack.
    /// </summary>
    public static UWObject CarriedItemAt(UWGameUI pOGameUi, UWInventory pOInventory, Vector2 pOScreenPosition)
    {
        int liContainerSlot = pOGameUi.Inventory.GetContainerSlotUnderMouse(pOScreenPosition);

        if (liContainerSlot >= 0)
            return pOGameUi.Inventory.GetContainerItem(liContainerSlot);

        int liBackpackSlot = pOGameUi.Inventory.GetBackpackSlotUnderMouse(pOScreenPosition);

        if (liBackpackSlot >= 0)
            return pOGameUi.Inventory.GetBackpackItem(liBackpackSlot);

        UWArmorItemMap.BodySlot? leZone = pOGameUi.Inventory.GetArmorHitZoneUnderMouse(pOScreenPosition);

        return leZone.HasValue && pOInventory != null
            ? pOInventory.GetEquipped(leZone.Value)
            : null;
    }
}
