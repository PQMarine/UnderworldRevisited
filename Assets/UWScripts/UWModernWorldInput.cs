using UnityEngine;
using UnityEngine.InputSystem;
using UWDataImport.UWData;

/// <summary>
/// THE MODERN SCHEME'S WAY INTO THE WORLD (cleanup step 5.14, 2026-10-22: out of Interaction, which
/// keeps the public surface and forwards): R and the pad's trigger draw the weapon, Q looks, E
/// tapped takes or uses and E held uses directly (the hold measured here), the free pointer's
/// click opens a thing's menu and its drag takes the thing, where E and Q aim (under the free
/// pointer or at the crosshair), whether the mouse buttons act in the world at all, and what the
/// crosshair names and what the pad's A would do there. The rules are the original's, the click
/// rules UWClickRules'; the classic scheme's clicks are UWClassicWorldGesture.
/// </summary>
public sealed class UWModernWorldInput
{
    private readonly Interaction mOInteraction;

    public UWModernWorldInput(Interaction pOInteraction)
    {
        mOInteraction = pOInteraction;
    }

    private UWDataImport.DataImport fData()
    {
        return mOInteraction.GameData;
    }

    private UWInventory fInventory()
    {
        return mOInteraction.Inventory;
    }

    /// <summary>Is the modern pointer free (UWModernPointer)?</summary>
    public bool IsPointerFree()
    {
        return mOInteraction.ControlScheme != null && mOInteraction.ControlScheme.IsPointerFree;
    }

    /// <summary>Do the mouse buttons act in the world? With the pointer locked, and - when the
    /// right button is held to look around and the pointer is free otherwise - also on the world
    /// outside the modern UI's windows (UWModernPointer.WorldTakesClicks); with the weapon drawn
    /// the free pointer on the world strikes too (per user, 2026-10-04 - it did nothing).</summary>
    public bool MouseInWorld()
    {
        return !IsPointerFree() || UWModernPointer.WorldTakesClicks()
            || (mOInteraction.IsCombatModeActive && UWModernPointer.FreePointerOnWorld());
    }

    /// <summary>
    /// THE MODERN SCHEME (concept per user, 2026-10-03): R draws or puts away the weapon (the
    /// left button only charges and strikes with it drawn - it drew too until the same day), Q
    /// looks at what the crosshair is on (the right button switches the pointer,
    /// UWModernPointer). E TAPPED does the default - what the classic right-button drag does
    /// (fModernUse); E HELD uses directly, even what could be picked up - a sack is emptied
    /// instead of taken (per user, the same day). The tap acts on the release, the hold once
    /// ModernUseHoldSeconds have passed. The rules are the original's; only the way to them is
    /// new. Nothing here while something hangs on the pointer.
    /// </summary>
    public void UpdateActions()
    {
        if (fInventory() != null && fInventory().CursorItem != null)
        {
            mOModernUse.Cancel();
            return;
        }

        // Only R draws the weapon: the left button no longer does (per user, 2026-10-03), so
        // outside combat it is free; drawn, it charges and strikes (Update, MouseInWorld).
        // The gamepad's right trigger draws the weapon as well (UWGamepad): held on, it charges
        // once the weapon is ready (Update). The mouse button does not draw.
        bool lbPadDraws = !mOInteraction.IsCombatModeActive && mOInteraction.Input.ModernAttack.WasPressedThisFrame()
            && UWGamepad.IsFromGamepad(mOInteraction.Input.ModernAttack) && !UWInput.PadPointerTakes(mOInteraction.Input.ModernAttack);

        if (mOInteraction.Input.ModernReady.WasPressedThisFrame() || lbPadDraws)
            mOInteraction.ToggleCombatMode();

        // THE FREE POINTER OVER THE INTERFACE (per user, 2026-10-07): Q looks at the thing in the
        // slot under the pointer - character panel, bags, action bar -, E tapped uses it there
        // and E held does nothing (fUpdateModernUseKey); until then both fell back to what the
        // crosshair aimed at in the world.
        bool lbOverUi = IsPointerFree() && UWPointer.HasMouse
            && UWModernUi.IsOverUi(UWPointer.Position);

        // Q looks (per user, 2026-10-03) - the right button switches the pointer
        // (UWModernPointer).
        // The gamepad's pointer takes its X as the right button (the PadPointer context, UWInput.PadPointerTakes).
        if (mOInteraction.Input.ModernLook.WasPressedThisFrame() && !UWInput.PadPointerTakes(mOInteraction.Input.ModernLook))
        {
            if (lbOverUi)
                mOInteraction.Look.LookAtUi(UWPointer.Position);
            else
            {
                fAimAtModern(out UWEntityInfo lOFrontmost, out string lsChunk, out UWEntityInfo _);
                mOInteraction.LookAtTarget(lOFrontmost, lsChunk);
            }
        }

        fUpdateModernUseKey(lbOverUi);

        fUpdateModernPointerWorld();
    }

    // ------------------------------------------------- The free pointer in the world

    /// <summary>
    /// THE FREE POINTER ON THE WORLD (per user, 2026-10-04, the MMO way): with the weapon put away,
    /// a left CLICK on a thing opens its MENU (TryOpenWorldMenu - on nothing, a wall or the floor
    /// nothing happens), held and DRAGGED it TAKES the thing onto the pointer (the bags open and it
    /// is put down where the button is let go - UWModernBags.BeginWorldDrag). The right button
    /// stays the pointer's everywhere outside the windows (a right click for the menu made it
    /// depend on what lay under the pointer). E and Q act under the free pointer as well
    /// (fAimAtModern). Not over a window of the modern UI, not with a thing on the pointer, not in
    /// the use mode, the split box or the menu.
    /// </summary>
    private void fUpdateModernPointerWorld()
    {
        if (!UWPointer.HasMouse || Camera.main == null || !IsPointerFree() || mOInteraction.IsCombatModeActive
            || UWModernBags.IsMenuOpen || UWModernBags.IsSplitting || UWModernQuestion.IsBlocking
            || (UWModernBags.Instance != null && UWModernBags.Instance.IsUsing))
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
                mOInteraction.AimAt(Camera.main.ScreenPointToRay(lOPointer), out mOWorldPressFront, out _, out mOWorldPressPickable);
            }

            return;
        }

        if (!mbWorldPress)
            return;

        bool lbMoved = Vector2.Distance(lOPointer, mOWorldPressAt) >= WorldDragPixels;

        if (UWMouseButtons.LeftHeld && lbMoved)
        {
            mbWorldPress = false;

            UWEntityInfo lOTake = mOWorldPressFront != null && mOWorldPressFront.CanBePickedUp ? mOWorldPressFront : mOWorldPressPickable;

            if (lOTake != null && UWWorldPickup.Take(mOInteraction, lOTake, UWWorldPickup.WholeStack, UWWorldPickup.Destination.Pointer)
                && UWModernBags.Instance != null)
                UWModernBags.Instance.BeginWorldDrag(mOWorldPressAt);

            return;
        }

        if (!UWMouseButtons.LeftHeld)
        {
            mbWorldPress = false;

            if (!lbMoved)
                TryOpenWorldMenu(mOWorldPressAt);
        }
    }

    /// <summary>Screen pixels the pointer moves before a press on the world becomes a drag.</summary>
    private const float WorldDragPixels = 6f;

    private bool mbWorldPress;

    private Vector2 mOWorldPressAt;

    private UWEntityInfo mOWorldPressFront;

    private UWEntityInfo mOWorldPressPickable;

    /// <summary>Where E and Q aim in the modern scheme: under the free pointer when it is on the
    /// world, else at the crosshair (per user, 2026-10-04).</summary>
    public Ray AimRay()
    {
        if (IsPointerFree() && UWPointer.HasMouse && Camera.main != null)
        {
            Vector2 lOPointer = UWPointer.Position;

            if (!UWModernUi.IsOverUi(lOPointer))
                return Camera.main.ScreenPointToRay(lOPointer);
        }

        return mOInteraction.InteractionRay();
    }

    private void fAimAtModern(out UWEntityInfo pOFrontmost, out string psChunk, out UWEntityInfo pOPickable)
    {
        mOInteraction.AimAt(AimRay(), out pOFrontmost, out psChunk, out pOPickable);
    }

    /// <summary>The thing a menu would be for under a screen point: the frontmost thing, or the
    /// pickable one behind geometry; not a wall panel (a piece of wall).</summary>
    private UWEntityInfo fWorldThingAt(Vector2 pOPointer, out UWEntityInfo pOPickableBehind)
    {
        pOPickableBehind = null;

        if (Camera.main == null)
            return null;

        mOInteraction.AimAt(Camera.main.ScreenPointToRay(pOPointer), out UWEntityInfo lOFront, out string _, out UWEntityInfo lOPickable);

        UWEntityInfo lOTarget = !Interaction.IsGeometryEntity(lOFront) ? lOFront : lOPickable;

        if (Interaction.IsGeometryEntity(lOTarget) || lOTarget.ObjectData.ID == WallPanelObjectId || lOTarget.ObjectData.ID == WallPanelObjectId + 1)
            return null;

        pOPickableBehind = lOTarget == lOFront ? lOPickable : null;
        return lOTarget;
    }

    /// <summary>Whether a thing's menu can open on this spot.</summary>
    public bool HasWorldThingAt(Vector2 pOPointer)
    {
        return UWModernUi.IsActive && (fInventory() == null || fInventory().CursorItem == null) && !mOInteraction.IsSpellTargeting
            && (UWModernBags.Instance == null || !UWModernBags.Instance.IsUsing)
            && fWorldThingAt(pOPointer, out UWEntityInfo _) != null;
    }

    /// <summary>A left click on a thing in the world with the free pointer: its menu
    /// (UWModernBags.OpenWorldMenu) - true if there was a thing.</summary>
    public bool TryOpenWorldMenu(Vector2 pOPointer)
    {
        if (!HasWorldThingAt(pOPointer) || UWModernBags.Instance == null)
            return false;

        UWEntityInfo lOTarget = fWorldThingAt(pOPointer, out UWEntityInfo lOPickable);
        bool lbCreature = Interaction.IsCreatureEntity(lOTarget);

        UWModernBags.Instance.OpenWorldMenu(lOTarget, lOPickable, lbCreature, lOTarget.CanBePickedUp && !lbCreature, pOPointer);
        return true;
    }

    /// <summary>The world menu's Use and Talk: the direct use of E held (fModernUseOn).</summary>
    public void ModernUseThing(UWEntityInfo pOTarget, UWEntityInfo pOPickableBehind)
    {
        fModernUseOn(true, pOTarget, pOPickableBehind);
    }

    /// <summary>How long E has to be held for the direct use.</summary>
    private const float ModernUseHoldSeconds = 0.4f;

    /// <summary>E's tap and hold (UWHoldTap since cleanup step 7.3, 2026-10-25; the time held, added up
    /// frame by frame, and a fired flag here until then): the hold due ModernUseHoldSeconds after the
    /// press by the unscaled clock, the press going on until the release, which taps only before the
    /// hold and only as a release.</summary>
    private readonly UWHoldTap mOModernUse = new UWHoldTap(ModernUseHoldSeconds, UWHoldTap.UseKey);

    /// <summary>How far the hold of E has come, 0 to 1, for the HUD; -1 while there is none to
    /// show (not held, just tapped, or already fired).</summary>
    public float ModernUseHoldProgress => mOModernUse.HeldSeconds >= 0.1f && !mOModernUse.HoldDone && !mbModernUseOnUi
        ? Mathf.Clamp01(mOModernUse.HeldSeconds / ModernUseHoldSeconds) : -1f;

    /// <summary>E went down with the free pointer over the interface: its tap uses the slot there,
    /// its hold does nothing.</summary>
    private bool mbModernUseOnUi;

    /// <summary>E tapped over the interface: the slot under the pointer is used, or nothing.</summary>
    private static void fUseAtUi(Vector2 pOPointer)
    {
        if (UWModernBags.Instance != null && UWModernBags.Instance.TryUseAt(pOPointer))
            return;

        if (UWModernActionBar.Instance != null)
            UWModernActionBar.Instance.TryUseAt(pOPointer);
    }

    /// <summary>Q over the interface alone, for a conversation, which holds the world: the free
    /// pointer over a window, nothing on the pointer.</summary>
    public void UpdateLookOverUi()
    {
        if ((fInventory() != null && fInventory().CursorItem != null) || !UWPointer.HasMouse
            || !mOInteraction.Input.ModernLook.WasPressedThisFrame() || UWInput.PadPointerTakes(mOInteraction.Input.ModernLook))
            return;

        Vector2 lOPointer = UWPointer.Position;

        if (UWModernUi.IsOverUi(lOPointer))
            mOInteraction.Look.LookAtUi(lOPointer);
    }

    private void fUpdateModernUseKey(bool pbOverUi)
    {
        InputAction lOKey = mOInteraction.Input.ModernUse;

        // The gamepad's pointer takes its A as the click (the PadPointer context, UWInput.PadPointerTakes).
        if (lOKey.WasPressedThisFrame() && UWInput.PadPointerTakes(lOKey))
        {
            mOModernUse.Cancel();
            return;
        }

        if (lOKey.WasPressedThisFrame())
        {
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

        switch (mOModernUse.Update(lOKey.IsPressed(), lOKey.WasReleasedThisFrame(), Time.unscaledTime))
        {
            case UWHoldTap.ResultEnum.Hold:
                // Held over the interface: nothing (per user, 2026-10-07).
                if (!mbModernUseOnUi)
                    fModernUse(true);

                break;
            case UWHoldTap.ResultEnum.Tap:
                // Released (not lost while a panel held the world): a tap that did not reach the hold.
                if (!mbModernUseOnUi)
                    fModernUse(false);
                else if (pbOverUi)
                    fUseAtUi(UWPointer.Position);

                break;
        }
    }

    /// <summary>
    /// The context key does WHAT THE CLASSIC RIGHT-BUTTON DRAG DOES (per user, 2026-10-03; the
    /// default gesture of UWClickRules): over something pickable the drag takes it - here
    /// straight into the backpack, as there is no pointer to put it on, with the reach of the
    /// hand ("That is too far away to take.") -; otherwise the release uses what was under the
    /// pointer: a creature is talked to, a door, switch, lever or container is used
    /// (UseTarget). Besides, a shrine takes the mantra and the chained princess answers as
    /// talking to her does (TalkToThing). On a wall, the floor or nothing: "You cannot use
    /// that.", as the original's use mode says there.
    ///
    /// pbDirect (E held): no taking - the thing is used as the original's use mode uses it, and
    /// what has no use stays where it is.
    /// </summary>
    private void fModernUse(bool pbDirect)
    {
        fAimAtModern(out UWEntityInfo lOFrontmost, out string _, out UWEntityInfo lOPickable);
        fModernUseOn(pbDirect, lOFrontmost, lOPickable);
    }

    private void fModernUseOn(bool pbDirect, UWEntityInfo lOFrontmost, UWEntityInfo lOPickable)
    {
        UWEntityInfo lOTake = lOFrontmost != null && lOFrontmost.CanBePickedUp ? lOFrontmost : lOPickable;

        if (!pbDirect && lOTake != null && !Interaction.IsCreatureEntity(lOTake))
        {
            UWWorldPickup.Take(mOInteraction, lOTake, UWWorldPickup.WholeStack, UWWorldPickup.Destination.Pack);

            return;
        }

        if (Interaction.IsGeometryEntity(lOFrontmost))
        {
            // Geometry in front with something pickable behind it (direct use only - the default
            // took it above): that thing.
            if (lOPickable != null)
                mOInteraction.UseTarget(lOPickable);
            else
                mOInteraction.AddGeneralMessage(UWClickRules.CannotUseThatMessage);

            return;
        }

        int liObjectId = lOFrontmost != null && lOFrontmost.ObjectData != null ? lOFrontmost.ObjectData.ID : -1;
        UWClickRules.ThingTalkAnswer leThing = UWClickRules.TalkToThing(liObjectId,
            lOFrontmost != null && lOFrontmost.IsChainedPrincess);

        if (!Interaction.IsCreatureEntity(lOFrontmost) && UWClickRules.ThingAnswers(leThing))
        {
            mOInteraction.TalkToThing(lOFrontmost);
            return;
        }

        mOInteraction.UseTarget(lOFrontmost);
    }

    /// <summary>The first of the two wall panels, 0x16E and 0x16F.</summary>
    private const int WallPanelObjectId = 0x16E;

    /// <summary>
    /// WHAT THE PAD'S A (E) DOES with what the crosshair is on, for the button hint under it
    /// (UWModernHud, per user 2026-10-08): fModernUseOn's tap without acting - "Pick up" what can be
    /// taken, "Talk" to a creature or a thing that answers, "Use" anything else - and null where
    /// the tap does nothing (a wall, the floor, beyond the sight).
    /// </summary>
    public string GetCrosshairPadVerb()
    {
        if (fData() == null)
            return null;

        mOInteraction.AimAtCrosshair(out UWEntityInfo lOFrontmost, out string _, out UWEntityInfo lOPickable);

        UWEntityInfo lOTake = lOFrontmost != null && lOFrontmost.CanBePickedUp ? lOFrontmost : lOPickable;

        if (lOTake != null && !Interaction.IsCreatureEntity(lOTake))
            return UWText.T("hud.crosshair.pick_up");

        if (Interaction.IsGeometryEntity(lOFrontmost))
            return null;

        if (Interaction.IsCreatureEntity(lOFrontmost))
            return UWText.T("hud.crosshair.talk");

        int liObjectId = lOFrontmost.ObjectData != null ? lOFrontmost.ObjectData.ID : -1;

        return UWClickRules.ThingAnswers(UWClickRules.TalkToThing(liObjectId, lOFrontmost.IsChainedPrincess))
            ? UWText.T("hud.crosshair.talk") : UWText.T("hud.crosshair.use");
    }

    /// <summary>
    /// WHAT THE CROSSHAIR IS ON, for the label under it (UWModernHud): the thing's bare
    /// name out of string block 4 ("goblin", "door"), plural for a stack. Without the look's
    /// side effects (identifying, trap search, look triggers); nothing for walls, floors,
    /// wall panels, geometry and what lies beyond the sight.
    /// </summary>
    public string GetCrosshairTargetName()
    {
        if (fData() == null)
            return null;

        mOInteraction.AimAtCrosshair(out UWEntityInfo lOFrontmost, out string _, out UWEntityInfo lOPickable);

        UWEntityInfo lOTarget = Interaction.IsGeometryEntity(lOFrontmost) ? lOPickable : lOFrontmost;

        if (Interaction.IsGeometryEntity(lOTarget))
            return null;

        // A WALL PANEL (0x16E/0x16F, "special tmap obj") is a piece of wall - the look describes
        // its wall texture; it gets no name, as a wall gets none (per user, 2026-10-03).
        if (lOTarget.ObjectData.ID == WallPanelObjectId || lOTarget.ObjectData.ID == WallPanelObjectId + 1)
            return null;

        // Plural for a real stack only (UWStackRules.IsStack): until 2026-10-11 any quantity
        // field above 1 chose the plural, so a special property named an enchanted piece as many.
        UWObject lOObject = lOTarget.ObjectData;

        return UWText.ObjectName(fData(), lOObject.ID, UWStackRules.IsStack(lOObject), false, null);
    }
}
