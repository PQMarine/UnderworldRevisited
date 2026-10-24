using UnityEngine;
using UnityEngine.InputSystem;
using UWDataImport.UWData;

/// <summary>
/// THE MODERN SCHEME'S ANSWERS (IUWUiScheme, cleanup 5.19, 2026-10-23): the view filling the
/// screen, the modern HUD, bags and panels (UWModernUi) and the pointer locked for the mouse look
/// unless freed (UWModernPointer). The presets Classic Wide and Classic Modular are this scheme
/// with the classic frame's picture (UWModernClassicFrame). Each answer is the code the asking
/// place ran itself while it tested the scheme, moved here as it was. One instance, with no state
/// of its own: it reads the scene through UWScene as the places did.
/// </summary>
public sealed class UWModernUiScheme : IUWUiScheme
{
    public static readonly UWModernUiScheme Instance = new UWModernUiScheme();

    private UWModernUiScheme()
    {
    }

    public UWUiSchemePolicy Policy => UWUiSchemePolicy.Modern;

    /// <summary>The modern scroll's paper while it is shown (UWModernScroll.Rows), else the
    /// modern message box's lines (Interaction.ModernMessageCount).</summary>
    public int ShownLogLines => UWModernLayout.IsScrollShown ? UWModernScroll.Rows : Interaction.ModernMessageCount;

    /// <summary>The menu, the big map, or a link of UWModernUi's closable chain (the waiting spell, the
    /// bags' use mode, the bags, the closable panels) - the same list Escape closes from.</summary>
    public bool PadBackCloses
    {
        get
        {
            UWGameUI lOUi = UWScene.GameUi;

            return UWModernUi.IsMenuOpen || (lOUi != null && lOUi.Map != null && lOUi.Map.IsMapVisible) || UWModernUi.HasClosable;
        }
    }

    /// <summary>The pointer hold of UWModernPointer, the bags and whatever else freed it
    /// (UWControlScheme.IsPointerFree).</summary>
    public bool IsPointerUnlocked
    {
        get
        {
            UWControlScheme lOScheme = UWScene.ControlScheme;

            return lOScheme != null && lOScheme.IsPointerFree;
        }
    }

    /// <summary>A window of the modern UI (UWModernUi.IsOverUi).</summary>
    public bool IsPointerOverInterface(Vector2 pOAt)
    {
        return UWModernUi.IsOverUi(pOAt);
    }

    /// <summary>The view fills the screen: nothing lies outside it.</summary>
    public bool IsPointerOutsideView(Vector2 pOAt)
    {
        return false;
    }

    /// <summary>The modern scheme: on the world, not on a window of its UI (a drag from a bag onto
    /// the world must not strike).</summary>
    public bool PressAimsAtWorld(bool pbFromPad)
    {
        Interaction lOInteraction = UWScene.Interaction;

        return pbFromPad || (lOInteraction != null && lOInteraction.MouseInWorld());
    }

    /// <summary>
    /// THE MODERN SCHEME takes the view's height for the pointer's third (per user, 2026-10-03,
    /// after trying the mouse movement while winding up - the view turned with it and the kind
    /// came out unreliably): looking up an overhead bash, down a thrust, straight a slash,
    /// fixed when the button goes down, as in the original.
    ///
    /// WITH THE POINTER FREE the original's thirds again, of the screen - the modern view fills
    /// it (per user, 2026-10-04): upper third an overhead bash, lower a thrust, between a slash.
    /// </summary>
    public UWAttackKindSource AttackKindSource
    {
        get
        {
            Interaction lOInteraction = UWScene.Interaction;

            return lOInteraction != null && lOInteraction.IsPointerFree() && Mouse.current != null
                ? UWAttackKindSource.PointerThirdsOnScreen
                : UWAttackKindSource.ViewPitch;
        }
    }

    /// <summary>
    /// THE MODERN SCHEME (per user, 2026-10-04): the right button switches the pointer there,
    /// so the LEFT button gives the target - through the crosshair, or under the free pointer;
    /// on a window of the modern UI the window has the click (a bag slot takes an item target,
    /// UWModernBags). See UWHudRunes.fAfterModernCast.
    /// </summary>
    public bool TryGetSpellTargetPoint(bool pbRightPressed, bool pbLeftPressed, out Vector2 pOAt)
    {
        pOAt = default;

        if (!pbLeftPressed)
            return false;

        Interaction lOInteraction = UWScene.Interaction;
        bool lbFree = lOInteraction != null && lOInteraction.IsPointerFree() && Mouse.current != null;

        pOAt = lbFree ? Mouse.current.position.ReadValue() : UWModernClassicFrame.ViewCentre;

        if (lbFree && UWModernUi.IsOverUi(pOAt))
            return false;

        return true;
    }

    /// <summary>The modern conversation's rows (UWModernConversation).</summary>
    public IUWConversationChoiceRows ConversationChoiceRows => UWModernConversation.Instance;

    /// <summary>The modern history scrolls with the stick while the pointer is on it (UWPadEntryStepper).</summary>
    public Rect ConversationHistoryRect => UWModernConversation.Instance != null
        ? UWModernConversation.Instance.HistoryScreenRect
        : default;

    /// <summary>The modern scheme has its own use mode in the bags (UWModernBags.BeginUse); without
    /// the bags the classic use mode takes the thing, as before.</summary>
    public bool TryBeginUseFromWorld(UWObject pOItem)
    {
        if (UWModernBags.Instance == null)
            return false;

        UWModernBags.Instance.BeginUse(pOItem, true);

        return true;
    }
}
