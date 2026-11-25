/// <summary>
/// WHAT THE MODERN INTERFACE ASKS OF THE WORLD AND THE GAME AROUND IT (Docs/UW2-PLAN.md, "The modern interface
/// for UW2: one interface, two hosts", stage U3 part C, 2026-11-23): what the crosshair is on, the screens that
/// cover the view, the fight mode, a spell being aimed, the map, the compass's step, and the few things the
/// windows start in the world. The first game's host answers from Interaction, UWGameUI and UWGameActions.
///
/// NOT HERE: walking, looking and striking with the mouse and the keys. That is each game's own world input
/// (the first game's UWModernWorldInput, the second's in its world view and game screen), which is on the
/// game's side as the hosts are.
/// </summary>
public interface IUWModernWorld
{
    // ------------------------------------------------- What covers the view

    /// <summary>The big map lies open.</summary>
    bool IsMapOpen { get; }

    /// <summary>A conversation runs.</summary>
    bool IsConversationOpen { get; }

    // ------------------------------------------------- The crosshair

    /// <summary>The name of what the crosshair is on, null for nothing.</summary>
    string TargetName { get; }

    /// <summary>The crosshair is on a locked door or chest; the key that opens it when the character carries it,
    /// -1 when not.</summary>
    bool TryGetTargetLock(out int piKeyId);

    /// <summary>What the pad's use button would do with the target, for its hint; null for none.</summary>
    string TargetPadVerb { get; }

    /// <summary>How far the held use key has come, 0 to 1; negative while it is not held.</summary>
    float UseHoldProgress { get; }

    // ------------------------------------------------- The fight mode and a spell's aim

    bool IsFightMode { get; }

    void ToggleFightMode();

    /// <summary>A spell waits for its target or its aim.</summary>
    bool IsSpellAiming { get; }

    void CancelSpellAim();

    // ------------------------------------------------- A thing in the world (the context menu's entries)

    /// <summary>The game's use of a thing in the world, with the thing to take that lies behind it, if any.</summary>
    void UseThing(UWEntityInfo pOTarget, UWEntityInfo pOPickableBehind);

    /// <summary>The game's look at a thing in the world.</summary>
    void LookAt(UWEntityInfo pOTarget);

    /// <summary>The whole thing onto the pointer. True when it was taken.</summary>
    bool TakeToPointer(UWEntityInfo pOTarget);

    // ------------------------------------------------- The map, the compass, the character's doings

    /// <summary>The character carries the map (the minimap shows only then).</summary>
    bool CarriesMap { get; }

    /// <summary>Opens the big map. False when the game refuses.</summary>
    bool OpenMap();

    /// <summary>The painted map of the level and the player's place on it, null before it is made.</summary>
    IUWMinimapPictures Automap { get; }

    /// <summary>The view's direction in the compass's sixteen steps.</summary>
    int CompassStep { get; }

    /// <summary>The gargoyle's eyes after a hit, as the heading strip shows them (UW2 plan part U4d step 2; the strip
    /// read the first game's UWEyesDisplay itself until then).</summary>
    UWModernFoeEyes FoeEyes { get; }

    void Track();

    void Camp();

    // ------------------------------------------------- The game menu and the pointer

    /// <summary>The game menu may not open now; the game has said why.</summary>
    bool RefusesMenu();

    /// <summary>The game may not be saved now; the game has said why.</summary>
    bool RefusesToSave();

    /// <summary>The system pointer shows (true) or the carried thing alone shows where one points (false).</summary>
    void ShowPointerForBags(bool pbShow);

    // ------------------------------------------------- Stage U3 part D

    /// <summary>The mouse buttons act in the world now, not on a window of the interface.</summary>
    bool IsMouseInWorld { get; }

    /// <summary>The modern pointer is free (not locked to the view's middle).</summary>
    bool IsPointerFree { get; }

    /// <summary>The windows take no thing and open no menu now (the second game's mous_in_panel: paralysed, or
    /// asleep in the Void).</summary>
    bool HoldsPanels { get; }

    /// <summary>The character can make camp here and now.</summary>
    bool CanCamp { get; }

    /// <summary>The game has making camp and reading tracks from the character panel's buttons at all (the panel
    /// shows a button only then).</summary>
    bool OffersCamp { get; }

    bool OffersTrack { get; }

    // ------------------------------------------------- The game menu's saves

    /// <summary>The name a save slot shows.</summary>
    string SaveSlotName(int piSlot);

    bool HasSave(int piSlot);

    /// <summary>Writes the game into a slot under a name. Null when it went well, else what went wrong.</summary>
    string WriteSave(int piSlot, string psName);

    /// <summary>The game says how the save went (WriteSave's answer).</summary>
    void ReportSave(string psError);

    /// <summary>The game says that the slot holds no save.</summary>
    void ReportNoSave();

    void LoadSave(int piSlot);

    void QuitToMainMenu();
}

/// <summary>
/// THE GARGOYLE'S EYES AFTER A HIT (IUWModernWorld.FoeEyes): lit while they show or fade, their picture now and how
/// strongly, the foe's name, its condition (0 healthy to 2 critical), and the head's row the picture's top lies on - the
/// games' eyes pictures differ (the first game's EYES.GR row 3 of the head, the second game's row 4).
/// </summary>
public struct UWModernFoeEyes
{
    public bool IsLit;

    public UnityEngine.Texture2D Picture;

    public float Alpha;

    public string Name;

    public int Group;

    public int TopInHead;
}

/// <summary>
/// THE MINIMAP'S PICTURES PER GAME (IUWModernWorld.Automap; UW2 plan part U4d step 3, 2026-11-23). A SECOND WAY BESIDE
/// THE FIRST GAME'S, for a reason: the first game's map comes as the parchment, the discovered tiles apart on a clear
/// ground and the notes (UWAutomapPictures, UWAutomapView), the second game paints the whole map screen at once in its
/// own palette (UWMapScreenUw2.ComposeScreen, UWAutomapPainter) - its tiles are no picture of their own. So each game
/// hands the minimap its pictures; the window (UWModernMinimap) is the first game's for both.
/// </summary>
public interface IUWMinimapPictures
{
    /// <summary>The middle of the character's tile on the 320 x 200 map screen, from the top-left; false without a level.</summary>
    bool TryGetPlayerPixel(out UnityEngine.Vector2 pOPixel);

    /// <summary>The discovered tiles of the level the character stands on, freshly drawn; pbPlayerMark the game's own
    /// mark of where the character stands (the minimap leaves it out, its arrow says it).</summary>
    UnityEngine.Texture2D BuildTiles(bool pbPlayerMark);

    /// <summary>Where BuildTiles' picture lies on the map screen (pixels, from the top-left).</summary>
    UnityEngine.Rect TilesRect { get; }

    /// <summary>The blank map under the tiles, null where the tiles' picture is the whole screen.</summary>
    UnityEngine.Texture2D GetParchment();

    /// <summary>The notes of the level the character stands on, as the map shows them.</summary>
    System.Collections.Generic.List<UWAutomapPictures.Note> GetNotes();
}
