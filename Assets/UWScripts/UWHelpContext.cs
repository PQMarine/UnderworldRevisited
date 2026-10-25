using UnityEngine;

/// <summary>
/// WHAT THE HELP WINDOW HANDS ITS TABS IN ONE PASS (cleanup 8.6, 2026-10-24): the scale of the layout
/// area (for the tabs that place a picture on whole screen pixels) and the clip at the sliding frame's
/// edge. These were the window's own fields (mfScale and the others) while it drew every tab itself.
/// Until cleanup 8b.4 also what the IMGUI form needed: the styles, the area's screen origin, the
/// panel's place and the tab that asked for the whole-pixel pass after the layout.
/// </summary>
public sealed class UWHelpContext
{
    /// <summary>The window, for the notes' Tab that closes it.</summary>
    public UWHelpWindow Window { get; }

    /// <summary>Scale of the layout area, in the scaled GUI units.</summary>
    public float Scale { get; internal set; } = 1f;

    /// <summary>The left edge in screen pixels the help is cut off at - the sliding frame's
    /// right edge beside the classic frame (UWHelpWindow.fPlace).</summary>
    public float ClipPixels { get; internal set; }

    public UWHelpContext(UWHelpWindow pOWindow)
    {
        Window = pOWindow;
    }
}
