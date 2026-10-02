using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The one owner of Time.timeScale. Until 2026-09-18 it was written at twenty places in eight
/// files: every screen set it to zero when it opened and to one when it closed, two of them
/// remembered "what it was before" so as not to trample another pause, and UWLevelLoader.Start
/// had to force it back to one because whoever left it at zero before a scene rebuild froze
/// the new scene (per user, 2026-09-11).
///
/// Now every pause is a named hold. The world stands still while any hold exists and runs
/// again when the last one is released - the paging hold ending under an open options panel
/// leaves the world frozen, as the remembered value did before. A scene start releases all
/// holds at once: the statics survive the scene reload, the screens that held them do not.
/// The screens run on the unscaled clock, as before.
/// </summary>
public static class UWGameClock
{
    public const string PagingHold = "paging";

    public const string OptionsHold = "options";

    public const string MainMenuHold = "main menu";

    public const string CharacterCreationHold = "character creation";

    public const string DeathHold = "death";

    public const string VictoryHold = "victory";

    /// <summary>A conversation is a modal loop in the original: the game loop, and with it the
    /// palette cycling of the spell icons, the water, the creatures and the weapon, stands still
    /// until it returns (per user on the original, 2026-09-18).</summary>
    public const string ConversationHold = "conversation";

    /// <summary>Held from the scene start until a game is started - the player has no
    /// floor to stand on yet.</summary>
    public const string NoWorldHold = "no world";

    private static readonly HashSet<string> msHolds = new HashSet<string>();

    public static bool IsHeld
    {
        get { return msHolds.Count > 0; }
    }

    public static void Hold(string psReason)
    {
        msHolds.Add(psReason);
        fApply();
    }

    public static void Release(string psReason)
    {
        msHolds.Remove(psReason);
        fApply();
    }

    /// <summary>A fresh start: the scene is rebuilt or a game begins, nothing from before
    /// may keep the world frozen.</summary>
    public static void ReleaseAll()
    {
        msHolds.Clear();
        fApply();
    }

    private static void fApply()
    {
        Time.timeScale = msHolds.Count > 0 ? 0f : 1f;
    }
}
