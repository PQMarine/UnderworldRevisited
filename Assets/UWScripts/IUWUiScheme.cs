using UnityEngine;
using UWDataImport.UWData;

/// <summary>
/// THE INTERFACE SCHEME THE GAME LOGIC TALKS TO (cleanup 5.19, 2026-10-23): one object through
/// which Interaction, the prompts, the conversation, the waiting spell, the weapon, the item drag
/// and the world use ask what the interface in force does - instead of testing the scheme
/// themselves (UWControlScheme.IsModern, 83 places until then) and branching. UWClassicUiScheme
/// answers as the original's screen does, UWModernUiScheme as the modern HUD does; UWControlScheme
/// swaps them on a scheme change (UWControlScheme.Ui) and keeps the hand-over of an open menu.
///
/// The decisions that need no engine are a table, UWUiSchemePolicy (engine-free, pinned by the
/// self-check); the members here are the questions whose answer lies in the running interface -
/// what is open, where the pointer is, what the scheme's windows take. Screen points and rects are
/// Unity's (Vector2, Rect), as the parts they come from; everything else is a plain type, so a
/// third front end (decision 26: Unreal, SDL) implements the same questions.
/// </summary>
public interface IUWUiScheme
{
    /// <summary>The scheme's decisions that need no engine (UWUiSchemePolicy.Classic or Modern).</summary>
    UWUiSchemePolicy Policy { get; }

    /// <summary>The lines of the message scroll in use now, which a long text pages by (decision
    /// 17, DECIDED 2026-10-20 per user; UWMessageLogModel.ShowNextPage): the classic box's five,
    /// the modern scroll's paper (UWModernScroll.Rows, three to nine), the modern message box's
    /// five (Interaction.ModernMessageCount).</summary>
    int ShownLogLines { get; }

    /// <summary>The pointer was freed from the mouse look (UWModernPointer: the right button, R3,
    /// the bags); the classic scheme has no lock, its pointer was never freed.</summary>
    bool IsPointerUnlocked { get; }

    /// <summary>Whether a screen point lies on the interface rather than on the world: outside the
    /// classic view window, or on a window of the modern UI (UWModernUi.IsOverUi).</summary>
    bool IsPointerOverInterface(Vector2 pOAt);

    /// <summary>Whether a screen point lies outside the world's view, where no world raycast is
    /// made: outside the classic view window; never in the modern scheme, whose view fills the
    /// screen (a window over it is IsPointerOverInterface's question).</summary>
    bool IsPointerOutsideView(Vector2 pOAt);

    /// <summary>Whether the weapon button's press aims at the world: in the classic view window;
    /// in the modern scheme from the pad, or with the mouse acting in the world
    /// (Interaction.MouseInWorld - not on a window of its UI, so a drag from a bag onto the world
    /// does not strike).</summary>
    bool PressAimsAtWorld(bool pbFromPad);

    /// <summary>What the kind of a blow is read from when the button goes down
    /// (UWPlayerAttackInput.fChooseAttackKind).</summary>
    UWAttackKindSource AttackKindSource { get; }

    /// <summary>
    /// The click that gives a waiting spell its target this frame (UWPendingSpell.UpdateTargeting):
    /// the screen point the target spell is applied to or the projectile flies towards, or false
    /// while the spell keeps waiting. The classic scheme takes the right button in the view
    /// window; the modern one the left button under the free pointer or through the crosshair, not
    /// on a window of its UI.
    /// </summary>
    bool TryGetSpellTargetPoint(bool pbRightPressed, bool pbLeftPressed, out Vector2 pOAt);

    /// <summary>The answer rows a click chooses from: the classic scroll's lines or the modern
    /// conversation's (UWConversationController); null while no conversation shows them.</summary>
    IUWConversationChoiceRows ConversationChoiceRows { get; }

    /// <summary>The conversation history the walk stick scrolls while the pad's pointer is on it
    /// (UWPadEntryStepper); an empty rect where the history does not scroll.</summary>
    Rect ConversationHistoryRect { get; }

    /// <summary>The scheme's own use mode for a thing standing in the world (the anvil): true if
    /// it took the thing (the modern bags, UWModernBags.BeginUse); false and the classic use mode
    /// hangs the thing's picture on the pointer (UWClassicUseMode.BeginFromWorld).</summary>
    bool TryBeginUseFromWorld(UWObject pOItem);
}
