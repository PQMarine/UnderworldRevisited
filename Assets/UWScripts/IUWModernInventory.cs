using UnityEngine;
using UWDataImport.UWData;

/// <summary>
/// WHAT THE MODERN INTERFACE ASKS OF THE INVENTORY (Docs/UW2-PLAN.md, "The modern interface for UW2: one
/// interface, two hosts", stage U2, 2026-11-23). THE RULES ARE THE MODEL'S: UWInventoryModel is engine-free and
/// both games run it, so the windows hold it directly - the pointer's object, the slots, the drags and drops,
/// the weight, a container's refusal. What reaches past the model into the game is here: using a carried
/// thing, looking at it, a spell waiting for it, and the pointer's object leaving for the world.
///
/// In Unity's assembly for the screen point of the two world actions; the rest names only data types.
/// </summary>
public interface IUWModernInventory
{
    /// <summary>The character's inventory, null before a character is loaded.</summary>
    UWInventoryModel Model { get; }

    // ------------------------------------------------- Using

    /// <summary>The game can use things now (its use host stands).</summary>
    bool CanUse { get; }

    /// <summary>The game's own use of a carried thing from where the windows ask (UWCarriedItemUse's chain: the
    /// map, a book, what works at once, what asks for a target). True when something happened.</summary>
    bool Use(UWObject pOItem, UWCarriedItemUse.Context peContext);

    /// <summary>The held thing of the use mode onto a carried thing. True when the use mode stays (the pole).</summary>
    bool UseOn(UWObject pOItem, UWObject pOCarriedTarget);

    /// <summary>The held thing of the use mode onto what lies under a screen point in the world.</summary>
    void UseOnWorld(UWObject pOItem, Vector2 pOScreenPoint);

    /// <summary>The line a thing says when its use mode begins ("Use the key on what?").</summary>
    void SayUsePrompt(UWObject pOItem);

    /// <summary>A carried container was opened in a window of its own: what opening sets off in the game happens
    /// here (the second game's UseObj runs the thing's traps and the spell it holds; nothing on the first).</summary>
    void ContainerOpened(UWObject pOContainer);

    /// <summary>The pointer's object dropped on the head: eaten or drunk if it can be. True when it was taken
    /// (a stack gives one; what is left stays on the pointer).</summary>
    bool TryConsumePointerItem();

    /// <summary>The pointer's object released over the view: laid down or thrown as the game does it. True
    /// when it left the pointer.</summary>
    bool DropOrThrowPointerItem(Vector2 pOScreenPoint);

    // ------------------------------------------------- Looking

    /// <summary>What a look at a carried thing says, as one text (the conversation shows it in its own place).</summary>
    string Describe(UWObject pOItem);

    /// <summary>A book or scroll is read; pbLooking for the look, which reads too. True when it was one.</summary>
    bool TryRead(UWObject pOItem, bool pbLooking);

    /// <summary>The look's lines into the game's messages.</summary>
    void SayLook(UWObject pOItem);

    // ------------------------------------------------- A spell waiting for a thing

    /// <summary>A spell waits for a carried thing to be clicked (Name Enchantment).</summary>
    bool IsItemSpellPending { get; }

    /// <summary>The click while such a spell waits, by the modern rule: on no thing it keeps waiting.</summary>
    UWPendingSpellState.InventoryClickOutcome CastPendingSpellOn(UWObject pOItem);
}
