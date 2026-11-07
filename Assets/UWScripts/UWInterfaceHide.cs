using UWDataImport.UWData;

/// <summary>
/// THE INTERFACE HIDDEN (per user, 2026-11-07): H in the modern scheme hides every part of the interface and
/// the weapon, for a clear view of the world - the modern HUD with its weapon and crosshair, the action bar,
/// the minimap, a pinned rune or character panel, the pad's hints; in the second game its modern HUD. H
/// again brings it back, and so does anything that wants the interface: a window or menu, a conversation,
/// the free pointer, typed input, the layout editor (Release). Only where the world fills the screen: the
/// classic scheme draws the view in the frame's hole, and so does Classic Wide, so neither hides anything.
/// The state is not saved; a new session starts with the interface shown.
/// </summary>
public static class UWInterfaceHide
{
    /// <summary>The interface is hidden now: the modern parts leave their canvases off.</summary>
    public static bool IsHidden { get; private set; }

    /// <summary>H was pressed where hiding is allowed: hidden becomes shown and the other way round.</summary>
    public static void Toggle()
    {
        IsHidden = !IsHidden;
    }

    /// <summary>Something wants the interface (pbWanted): it comes back. Called every frame by the
    /// modern HUD of the game in play.</summary>
    public static void Release(bool pbWanted)
    {
        if (pbWanted)
            IsHidden = false;
    }

    /// <summary>The input contexts that want the interface: a modal hold (a menu, a window, the map),
    /// a conversation, typing, a screen menu, a cutscene.</summary>
    public const UWInputContext WantingContexts = UWInputContext.UiModal | UWInputContext.Conversation
        | UWInputContext.TextEntry | UWInputContext.ScreenMenu | UWInputContext.Cutscene;
}
