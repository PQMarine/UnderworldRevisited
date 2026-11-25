using UnityEngine;
using UWDataImport;
using UWDataImport.UWData;

/// <summary>
/// THE RUNNING GAME AS THE MODERN INTERFACE SEES IT (Docs/UW2-PLAN.md, "The modern interface for UW2: one
/// interface, two hosts", per user 2026-11-23): the one entry the modern windows ask through, so they name no
/// game. It bundles narrow interfaces by domain; each game brings a host (UWModernSessionUw1; the second
/// game's in stage U4). Made when a game starts and set on UWModernUi.Session.
///
/// NOT UWGameSession: that is the engine-free state of a run (quests, variables, rules) for both games. A
/// host may read it, a window may not.
///
/// STAGE U1 (2026-11-23): the character's numbers, the active spells, the messages and the pictures that
/// follow the character's state. STAGE U2 (the same day): the inventory. STAGE U3: the game's data and filter for
/// every window (part A), the runes and the cast (part B), the world (part C).
/// </summary>
public interface IUWModernSession
{
    /// <summary>The game's data: the textures, palettes, fonts and strings the windows draw from.</summary>
    DataImport Data { get; }

    /// <summary>How the game's pictures are filtered (the Remastered and Original looks).</summary>
    FilterMode Filter { get; }

    /// <summary>The first game's interface, for the texts' fonts (UWUiKit.CreateText); null on the second, as
    /// IUWModernConversationHost.TextUi.</summary>
    UWGameUI TextUi { get; }

    IUWModernCharacter Character { get; }

    IUWModernInventory Inventory { get; }

    IUWModernMagic Magic { get; }

    IUWModernMessages Messages { get; }

    IUWModernWorld World { get; }

    IUWModernArt Art { get; }

    /// <summary>What the modern conversation stands on in this game (stage U3 part D; the first game's was made
    /// by UWModernConversation itself until then, the second's is given by its screen).</summary>
    IUWModernConversationHost Conversation { get; }

    /// <summary>
    /// WHICH PARTS OF THE MODERN INTERFACE THIS GAME HAS (rule 3 of the plan, stage U4 part A, 2026-11-23): the
    /// first game all of them. The second game gets them one by one as its host and its art come to stand behind
    /// each (UWModernSessionUw2); a part it does not have yet does not show, and its stopgap shows in its place.
    /// </summary>
    bool Has(UWModernPartEnum pePart);

    /// <summary>Whether the game has the pieces of an interface preset; one it lacks shows as Modern
    /// (UWModernLayout.Preset).</summary>
    bool Supports(UWModernLayout.PresetEnum pePreset);
}

/// <summary>The parts of the modern interface a game can have (IUWModernSession.Has): the HUD's pieces, the
/// windows of their own, and the HUD's own handling of the pointer and its clicks.</summary>
public enum UWModernPartEnum
{
    Weapon,
    Vitals,
    Messages,
    Crosshair,
    ActiveSpells,
    Heading,
    Compass,
    RuneHollow,
    StoneShelf,
    Backs,
    ViewPicture,
    StatsPage,
    CharacterPage,
    ClassicFrame,
    Bags,
    ActionBar,
    CharacterPanel,
    RunePanel,
    Minimap,
    Question,
    GameMenu,
    LayoutEditor,

    /// <summary>The HUD's clicks on its own parts (UWModernHudInput).</summary>
    HudInput,

    /// <summary>The HUD's switch of the pointer on the right button (UWModernPointer.Tick). A game whose screen
    /// still switches the pointer itself must not have it, or the button would switch twice.</summary>
    HudPointer,

    /// <summary>The gamepad's hint strip (UWPadHintStrip), which names the first game's windows.</summary>
    PadHints,

    /// <summary>The active spells in a hollow of the frame (the second game's left hollow; UW2 plan stage U6 step 2c).</summary>
    SpellHollow
}
