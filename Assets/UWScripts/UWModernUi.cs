using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// THE MODERN UI'S ROOT (cleanup 5.16, 2026-10-22): the questions every modern part asked in a
/// copy of its own, each with one home now, and the answers exactly what the copies gave.
///
///   - THE SCALE: screen pixels per original pixel and the text size that goes with it (until
///     then UWModernHud's statics).
///   - THE GATE: whether the modern HUD is on the screen (IsShowing), in a conversation
///     (IsTalking), its game menu open (IsMenuOpen) - the HUD component keeps the scene's parts it
///     asks; the parts and panels ask here instead of reaching into UWModernHud.Instance.
///   - THE POINTER GATE of the HUD's own buttons (HudTakesPointer).
///   - IsOverUi: whether a screen point lies on a window of the modern UI (UWModernPointer's until
///     then; the right button uses there instead of switching, the world takes no click there,
///     Shift does not toggle the slow walk there - UWPlayerInput).
///   - THE CLOSABLE CHAIN for Escape and the gamepad's B, in one order (IUWModernClosable).
///
/// A static class, not a component: the parts stay the components and plain classes they were,
/// in the order they were added (UWGameUI.Start), so nothing here moves an update or a canvas.
/// </summary>
public static class UWModernUi
{
    // ------------------------------------------------- The scale

    /// <summary>Screen rows per original pixel step of the HUD images: the integer scale is the
    /// screen height divided by this.</summary>
    private const float PixelScaleRows = 270f;

    /// <summary>Original pixels the bottom row needs across: the shelf (4 + 106), the action bar
    /// (196), the backpack with the panel's handle (86 + 9 + margins) and the gaps.</summary>
    private const float BottomRowWidth = 425f;

    /// <summary>
    /// The scale the screen gives (per user, 2026-10-03): one original pixel per 270 rows, but no
    /// more than lets the bottom row fit across - so a 4:3 or 5:4 screen gets a smaller one than a
    /// wide screen of the same height.
    /// </summary>
    public static float AutoPixelScale => Mathf.Max(1f, Mathf.Min(Screen.height / PixelScaleRows, Screen.width / BottomRowWidth));

    /// <summary>
    /// THE MODERN UI'S SCALE - screen pixels per original pixel, for the HUD, the bags, the
    /// character panel, the minimap and the action bar alike: the automatic one times the
    /// player's choice in the game menu (UWUserSettings.ModernUiPercent, 50 to 200 %). FREE, not
    /// only whole numbers: the pictures go through the pixel-art shaders (UWPixelArtUI,
    /// UWIconPalette.ApplySmooth), which keep every texel a hard square at any scale.
    /// </summary>
    public static float PixelScale => AutoPixelScale * (UiPercent / 100f);

    /// <summary>The chosen size, 100 when none was chosen.</summary>
    public static int UiPercent
    {
        get
        {
            int liPercent = UWUserSettings.ModernUiPercent;

            return liPercent <= 0 ? 100 : Mathf.Clamp(liPercent, MinUiPercent, MaxUiPercent);
        }
    }

    public const int MinUiPercent = 50;

    public const int MaxUiPercent = 200;

    public const int UiPercentStep = 10;

    /// <summary>The UI size in percent of the automatic one - the layout editor's - and +.</summary>
    public static void SetUiPercent(int piPercent)
    {
        UWUserSettings.ModernUiPercent = Mathf.Clamp(piPercent, MinUiPercent, MaxUiPercent);
        UWUserSettings.Save();
    }

    /// <summary>The text size that goes with the scale - 20 at 1080 lines, as before.</summary>
    public static int FontSize => Mathf.Max(10, Mathf.RoundToInt(5f * PixelScale));

    // ------------------------------------------------- The gate

    /// <summary>The modern scheme is the one in force (UWControlScheme), whatever shows: the one
    /// gate of the modern parts since cleanup 5.19 (2026-10-23) - each tested the scheme itself
    /// until then. The game logic does not ask here; it asks the scheme in force (UWControlScheme.Ui).</summary>
    public static bool IsActive => UWControlScheme.IsModern;

    /// <summary>The modern HUD is on the screen (UWModernHud.IsShowing): the modern scheme, no
    /// conversation, no screen menu or cutscene over it. False without a HUD.</summary>
    public static bool IsShowing => UWModernHud.Instance != null && UWModernHud.Instance.IsShowing;

    /// <summary>A conversation in the modern scheme (UWModernHud.IsTalking): the HUD hides, the
    /// bags and the character panel stay for the trade. False without a HUD.</summary>
    public static bool IsTalking => UWModernHud.Instance != null && UWModernHud.Instance.IsTalking;

    /// <summary>Shown or talking - where the bags and the character panel show.</summary>
    public static bool IsShowingOrTalking => UWModernHud.Instance != null
        && (UWModernHud.Instance.IsShowing || UWModernHud.Instance.IsTalking);

    /// <summary>The game menu is open (UWModernGameMenu.IsOpen), the layout editor included.
    /// False without a HUD.</summary>
    public static bool IsMenuOpen => UWModernHud.Instance != null && UWModernHud.Instance.IsOpen;

    /// <summary>The game menu, null without a HUD.</summary>
    public static UWModernGameMenu Menu => UWModernHud.Instance != null ? UWModernHud.Instance.Menu : null;

    /// <summary>The HUD takes the game's keys and buttons: shown, its menu shut, nothing typed.</summary>
    public static bool HudTakesInput => IsShowing && !IsMenuOpen && !UWControls.IsTextEntryActive;

    /// <summary>
    /// THE POINTER GATE of the HUD's own buttons - the active spells, the heading's and the
    /// compass's status click, the compass disc's arrows, the rune hollow - which each wrote it out
    /// until 2026-10-22: the HUD takes input (HudTakesInput), the pointer is free (UWModernPointer),
    /// a mouse is there and the HUD's control scheme has its controls. pOPointer is the mouse.
    /// pOHud is the HUD asking (the one of the scene).
    /// </summary>
    public static bool HudTakesPointer(UWModernHud pOHud, out Vector2 pOPointer, out UWControls pOControls)
    {
        pOPointer = Vector2.zero;
        pOControls = null;

        UWModernHud lOHud = pOHud;

        if (lOHud == null || !lOHud.IsShowing || lOHud.IsOpen || UWControls.IsTextEntryActive || !UWModernPointer.IsFree
            || Mouse.current == null || lOHud.Scheme == null || lOHud.Scheme.Controls == null)
            return false;

        pOPointer = Mouse.current.position.ReadValue();
        pOControls = lOHud.Scheme.Controls;

        return true;
    }

    // ------------------------------------------------- Over the UI

    /// <summary>Whether a screen point lies on a window of the modern UI - there the right button
    /// uses instead of switching (UWModernPointer), the world takes no click (UWModernWorldInput,
    /// Interaction's aim), Shift does not toggle the slow walk (UWPlayerInput), the pad's stick
    /// scrolls (UWGamepadPointer).</summary>
    public static bool IsOverUi(Vector2 pOPointer)
    {
        return (UWModernBags.Instance != null && UWModernBags.Instance.IsOverWindow(pOPointer))
            || (UWModernPanel.Instance != null && UWModernPanel.Instance.Contains(pOPointer))
            || (UWModernRunePanel.Instance != null && UWModernRunePanel.Instance.Contains(pOPointer))
            || (UWModernQuestion.Instance != null && UWModernQuestion.Instance.Contains(pOPointer))
            || (UWModernHud.Instance != null && UWModernHud.Instance.IsOverParts(pOPointer))
            || (UWModernMinimap.Instance != null && UWModernMinimap.Instance.Contains(pOPointer))
            || (UWModernActionBar.Instance != null && UWModernActionBar.Instance.ScreenRect.Contains(pOPointer))
            || (UWModernClassicFrame.Instance != null && UWModernClassicFrame.Instance.IsOverFrame(pOPointer));
    }

    // ------------------------------------------------- The closable chain

    /// <summary>A link of the chain that is no part of its own: a test and what closes it.</summary>
    private sealed class Closable : IUWModernClosable
    {
        private readonly Func<bool> mOIsClosable;

        private readonly Action mOClose;

        public Closable(Func<bool> pOIsClosable, Action pOClose)
        {
            mOIsClosable = pOIsClosable;
            mOClose = pOClose;
        }

        public bool IsClosable => mOIsClosable();

        public void Close()
        {
            mOClose();
        }
    }

    /// <summary>A spell waiting for its target or aim lets go (UWHudRunes.fAfterModernCast).</summary>
    private static readonly IUWModernClosable msPendingSpell = new Closable(
        () => UWScene.Interaction != null && UWScene.Interaction.IsSpellTargeting,
        () => UWScene.Interaction.CancelPendingSpell());

    /// <summary>The bags' use mode ends (UWModernBags.CancelUse).</summary>
    private static readonly IUWModernClosable msBagsUse = new Closable(
        () => UWModernBags.Instance != null && UWModernBags.Instance.IsUsing,
        () => UWModernBags.Instance.CancelUse());


    /// <summary>
    /// THE ORDER ESCAPE AND THE PAD'S B CLOSE THINGS IN, before the game menu (UWGameUI's
    /// routing): first a waiting spell lets go, then the bags' use mode ends, the bags close, the
    /// character panel (UWModernPanel; a pinned one only leaves its Help tab), the rune panel
    /// (UWModernRunePanel; not pinned, not the tablet alone) - the two docked panels are links
    /// themselves (UWModernDockedPanel), and so are the bags (UWModernBags, since cleanup 5.17).
    /// One list since 2026-10-22; before, the Escape routing and PadBackCloses each wrote it out.
    /// Each entry finds its link now, as the scene has it.
    /// </summary>
    private static readonly Func<IUWModernClosable>[] msClosables =
    {
        () => msPendingSpell,
        () => msBagsUse,
        () => UWModernBags.Instance,
        () => UWModernPanel.Instance,
        () => UWModernRunePanel.Instance
    };

    /// <summary>The link of an entry, null where the scene has none (a destroyed component counts
    /// as none, as Unity's own test says).</summary>
    private static IUWModernClosable fLink(Func<IUWModernClosable> pOEntry)
    {
        IUWModernClosable lOLink = pOEntry();

        return lOLink is UnityEngine.Object lOObject && lOObject == null ? null : lOLink;
    }

    /// <summary>Whether anything of the chain has something to close.</summary>
    public static bool HasClosable
    {
        get
        {
            foreach (Func<IUWModernClosable> lOEntry in msClosables)
            {
                IUWModernClosable lOLink = fLink(lOEntry);

                if (lOLink != null && lOLink.IsClosable)
                    return true;
            }

            return false;
        }
    }

    /// <summary>Closes the first link of the chain with something to close; false when none has.</summary>
    public static bool CloseNext()
    {
        foreach (Func<IUWModernClosable> lOEntry in msClosables)
        {
            IUWModernClosable lOLink = fLink(lOEntry);

            if (lOLink == null || !lOLink.IsClosable)
                continue;

            lOLink.Close();
            return true;
        }

        return false;
    }
}
