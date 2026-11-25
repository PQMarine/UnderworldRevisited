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
    // ------------------------------------------------- The running game

    /// <summary>
    /// THE RUNNING GAME AS THE MODERN WINDOWS SEE IT (IUWModernSession, Docs/UW2-PLAN.md "The modern interface
    /// for UW2: one interface, two hosts", stage U1, 2026-11-23): set by the game's host when its scene has its
    /// parts (the first game: UWModernHud.Start), null without a running game. The windows ask through it
    /// instead of reaching for the first game's components.
    /// </summary>
    public static IUWModernSession Session
    {
        get => msSession;
        set
        {
            msSession = value;

            // The running game's own layout and preset (part U4e step 3 of the UW2 plan).
            if (value != null && UWDataImport.UWData.UWGameSession.Current != null)
                UWUserSettings.UseLayoutOf(UWDataImport.UWData.UWGameSession.Current.Game.Game);
        }
    }

    private static IUWModernSession msSession;

    /// <summary>The running game's inventory model (Session.Inventory.Model, stage U2), null without a game or a
    /// character - what the windows held as UWInventory until then.</summary>
    public static UWDataImport.UWData.UWInventoryModel Inventory
        => Session != null && Session.Inventory != null ? Session.Inventory.Model : null;

    /// <summary>The running game's data (Session.Data, stage U3 part A, 2026-11-23), null without a game or before
    /// its data is read - what the windows read as UWGameUI.mOUWData until then.</summary>
    public static UWDataImport.DataImport Data => Session != null ? Session.Data : null;

    /// <summary>How the running game's pictures are filtered (Session.Filter; UWGameUI.TextureFilterMode until
    /// stage U3 part A), Point without a game as that field's default.</summary>
    public static FilterMode Filter => Session != null ? Session.Filter : FilterMode.Point;

    /// <summary>What the windows' texts take their font files from (Session.TextUi, UWUiKit.CreateText's argument;
    /// the windows' own UWGameUI fields until stage U3 part D), null without a game.</summary>
    public static UWGameUI TextUi => Session != null ? Session.TextUi : null;

    /// <summary>The running game has this part of the modern interface (Session.Has, stage U4 part A): false
    /// without a game, so a window gated by it needs no other check for one.</summary>
    public static bool Has(UWModernPartEnum pePart)
    {
        return Session != null && Session.Has(pePart);
    }

    /// <summary>The big map lies open (Session.World.IsMapOpen, stage U3 part C; UWGameUI.Map.IsMapVisible until then).</summary>
    public static bool IsMapOpen => Session != null && Session.World.IsMapOpen;

    /// <summary>A conversation runs (Session.World.IsConversationOpen; UWConversationController.IsAnyOpen until
    /// stage U3 part C).</summary>
    public static bool IsConversationOpen => Session != null && Session.World.IsConversationOpen;

    /// <summary>A spell waits for its target or aim (Session.World.IsSpellAiming; the second game's half asked its
    /// attack host here until part U4e step 1 of the UW2 plan, its host answers now); UWModernPointer.SpellHolds asked
    /// both itself until stage U3 part D.</summary>
    public static bool IsSpellWaiting => Session != null && Session.World.IsSpellAiming;

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
            || !UWPointer.HasMouse || lOHud.Scheme == null || lOHud.Scheme.Controls == null || IsHelpUnderPointer)
            return false;

        pOPointer = UWPointer.Position;
        pOControls = lOHud.Scheme.Controls;

        return true;
    }

    /// <summary>
    /// THE POINTER GATE OF THE KIT'S WINDOWS (cleanup 8b.1, 2026-10-24): the setup bar and the windows
    /// moved off IMGUI (UWKitRows) take their clicks here, as the HUD's buttons at HudTakesPointer: a
    /// mouse is there and the pointer is not in the modern look mode (UWPointer.IsLookLocked; the
    /// system pointer's lock until cleanup step 7.7B). pOPointer is UWPointer's position (screen pixels,
    /// y up): ours in the game, the pad's where it placed the free pointer and was used last, else the
    /// system's. The presses are UWPointer's left button - the pad's A presses its own mouse's, so they
    /// arrive there as well.
    /// </summary>
    public static bool WindowTakesPointer(out Vector2 pOPointer)
    {
        pOPointer = Vector2.zero;

        if (!UWPointer.HasMouse || UWPointer.IsLookLocked)
            return false;

        pOPointer = UWPointer.Position;

        return true;
    }

    // ------------------------------------------------- Over the UI

    /// <summary>
    /// THE HELP LIES ON THE POINTER (per user and a Reddit report, 2026-11-02: a click on the help took the
    /// leather vest from the inventory page under it): its own area (UWHelpWindow, IsBlockedByHelp), and the
    /// character panel around it with the tab row while the panel holds it. The modern windows read their
    /// clicks from the actions, past UWMouseButtons, so they ask here.
    /// </summary>
    public static bool IsHelpUnderPointer => UWMouseButtons.IsBlockedByHelp
        || (UWHelpLayout.IsOpen && UWModernPanel.HoldsHelp && UWPointer.HasMouse && UWModernPanel.Instance.Contains(UWPointer.Position));

    /// <summary>Whether a screen point lies on a window of the modern UI - there the right button
    /// uses instead of switching (UWModernPointer), the world takes no click (UWModernWorldInput,
    /// Interaction's aim), Shift does not toggle the slow walk (UWPlayerInput), the pad's stick
    /// scrolls (UWGamepadPointer).</summary>
    public static bool IsOverUi(Vector2 pOPointer)
    {
        return UWMouseButtons.IsBlockedByHelp
            || (UWModernBags.Instance != null && UWModernBags.Instance.IsOverWindow(pOPointer))
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

    /// <summary>A spell waiting for its target or aim lets go (IUWModernWorld.CancelSpellAim: the first game's
    /// UWHudRunes.fAfterModernCast, the second game's attack host's missile or target spell).</summary>
    private static readonly IUWModernClosable msPendingSpell = new Closable(
        () => Session != null && Session.World.IsSpellAiming,
        () => Session.World.CancelSpellAim());

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
