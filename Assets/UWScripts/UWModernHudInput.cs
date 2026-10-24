using UnityEngine;
using UWDataImport;
using UWDataImport.UWData;

/// <summary>
/// THE MODERN HUD'S BUTTONS (out of UWModernHud, cleanup 5.16, 2026-10-22): what a click on the
/// HUD's parts does, in the order the HUD's Update asked them - the heading's and the compass's
/// status, the active spells, the compass disc's movement arrows, the rune hollow, the original's
/// stats page. Every one behind the HUD's pointer gate (UWModernUi.HudTakesPointer), which each
/// method wrote out in a copy of its own before. A plain class the HUD makes with its parts and
/// asks once a frame, after the right button's pointer switch (UWModernPointer.Tick).
/// </summary>
public class UWModernHudInput
{
    private readonly UWModernHud mOHud;

    private readonly UWModernActiveSpells mOSpells;

    private readonly UWModernHeadingStrip mOHeading;

    private readonly UWModernCompass mOCompass;

    private readonly UWModernRuneHollow mORuneHollow;

    private readonly UWModernStatsPage mOStats;

    private UWGameUI mOUi => mOHud.Ui;

    private UWControlScheme mOScheme => mOHud.Scheme;

    /// <summary>Whether the button held on the compass disc's arrows was pressed there.</summary>
    private bool mbArrowPress;

    /// <summary>The command of each arrow, as the classic frame's (UWHudCompass).</summary>
    private static readonly int[] miArrowCommands =
    {
        UWEasyMovement.CommandTurnLeft,
        UWEasyMovement.CommandStepForward,
        UWEasyMovement.CommandTurnRight
    };

    public UWModernHudInput(UWModernHud pOHud, UWModernActiveSpells pOSpells, UWModernHeadingStrip pOHeading, UWModernCompass pOCompass,
        UWModernRuneHollow pORuneHollow, UWModernStatsPage pOStats)
    {
        mOHud = pOHud;
        mOSpells = pOSpells;
        mOHeading = pOHeading;
        mOCompass = pOCompass;
        mORuneHollow = pORuneHollow;
        mOStats = pOStats;
    }

    /// <summary>Once a frame, in the HUD's Update.</summary>
    public void Update()
    {
        fUpdateSpellClicks();
        fUpdateMoveArrowClicks();
        fUpdateRuneHollowClicks();

        if (mOStats != null && mOHud.IsShowing && !mOHud.IsOpen && !UWControls.IsTextEntryActive && mOScheme != null)
            mOStats.UpdateClicks(mOScheme.Controls);
    }

    /// <summary>The pointer free over an active spell: the left button names it in the messages,
    /// the right button ends it (as one takes a buff away in an MMO; the classic page has the two
    /// the other way round).</summary>
    private void fUpdateSpellClicks()
    {
        mOSpells.HoveredSpell = -1;
        mOHeading.IsHovered = false;

        if (!UWModernUi.HudTakesPointer(mOHud, out Vector2 lOAt, out UWControls lOControls) || mOUi.Runes == null
            || UWModernPointer.PanelsHeld)
            return;

        // The heading: the status, as the classic compass's click - and the original's compass
        // the same (UWModernCompass).
        mOHeading.IsHovered = mOHeading.ScreenRect.Contains(lOAt);

        // On the stone disc's arrows the press walks instead (fUpdateMoveArrowClicks).
        bool lbOnCompass = mOCompass != null && mOCompass.ScreenRect.Contains(lOAt) && mOCompass.ArrowAt(lOAt) < 0;

        if ((mOHeading.IsHovered || lbOnCompass) && lOControls.Player.CursorDrag.WasPressedThisFrame())
        {
            mOUi.Actions.ReportStatus();
            return;
        }

        int liSlot = mOSpells.SpellAt(lOAt);

        if (liSlot < 0)
            return;

        mOSpells.HoveredSpell = liSlot;

        if (lOControls.Player.CursorDrag.WasPressedThisFrame())
            mOUi.Runes.DescribeActiveSpell(liSlot);
        else if (lOControls.Player.Interact.WasPressedThisFrame())
            mOUi.Runes.CancelActiveSpell(liSlot);
    }

    /// <summary>A left click anywhere in the hollow casts what lies in it, as on the frame
    /// (UWHudRunes.fUpdateSpellCast: the right button does nothing, not while the view roams) -
    /// with the pointer free.</summary>
    private void fUpdateRuneHollowClicks()
    {
        if (!UWModernUi.HudTakesPointer(mOHud, out Vector2 lOAt, out UWControls lOControls) || mORuneHollow.ScreenRect.width <= 0f
            || mOUi.Runes == null || UWRoamingSight.IsAnyRunning)
            return;

        if (lOControls.Player.CursorDrag.WasPressedThisFrame() && mORuneHollow.ScreenRect.Contains(lOAt))
            mOUi.Actions.CastShelf();
    }

    /// <summary>
    /// THE MOVEMENT ARROWS ON THE COMPASS'S STONE DISC (UWModernCompass.ArrowAt) work as on the
    /// classic frame (UWClassicWorldGesture.TryEasyMovement): either mouse button held on one steps or
    /// turns and repeats (UWPlayerInput.HoldEasyMovement) - with the pointer free, and only
    /// for a press that began on the arrows, so a drag from elsewhere passing over them does not
    /// walk. (The arrows as an element of their own, 2026-10-10, went again the same day, per
    /// user: with the disc they are not needed, and alone they looked lost.)
    /// </summary>
    private void fUpdateMoveArrowClicks()
    {
        if (!UWModernUi.HudTakesPointer(mOHud, out Vector2 lOAt, out UWControls lOControls) || mOCompass == null)
        {
            mbArrowPress = false;
            return;
        }

        bool lbHeld = lOControls.Player.CursorDrag.IsPressed() || lOControls.Player.Interact.IsPressed();
        int liArrow = mOCompass.ArrowAt(lOAt);

        if (!lbHeld)
        {
            mbArrowPress = false;
            return;
        }

        if (lOControls.Player.CursorDrag.WasPressedThisFrame() || lOControls.Player.Interact.WasPressedThisFrame())
            mbArrowPress = liArrow >= 0;

        if (!mbArrowPress || liArrow < 0 || UWScene.PlayerMovement == null)
            return;

        UWScene.PlayerMovement.Input.HoldEasyMovement(miArrowCommands[liArrow]);
    }
}
