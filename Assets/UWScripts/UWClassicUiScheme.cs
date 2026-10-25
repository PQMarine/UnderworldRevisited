using UnityEngine;
using UnityEngine.InputSystem;
using UWDataImport.UWData;

/// <summary>
/// THE CLASSIC SCHEME'S ANSWERS (IUWUiScheme, cleanup 5.19, 2026-10-23): the original's screen -
/// the frame with its view window, the paperdoll and the scroll (UWClassicHud) - and the pointer
/// free in it as in 1992. Each answer is the code the asking place ran itself while it tested
/// the scheme, moved here as it was. One instance, with no state of its own: it reads the scene
/// through UWScene as the places did.
/// </summary>
public sealed class UWClassicUiScheme : IUWUiScheme
{
    public static readonly UWClassicUiScheme Instance = new UWClassicUiScheme();

    private UWClassicUiScheme()
    {
    }

    public UWUiSchemePolicy Policy => UWUiSchemePolicy.Classic;

    private static UWGameUI fUi()
    {
        return UWScene.GameUi;
    }

    /// <summary>The classic box's lines (UWHudMessageLog.LogLineCount); the model's fallback
    /// without a UI.</summary>
    public int ShownLogLines
    {
        get
        {
            UWGameUI lOUi = fUi();

            return lOUi != null ? lOUi.MessageLog.LogLineCount : UWMessageLogModel.FallbackCapacity;
        }
    }

    /// <summary>Never locked, so never freed.</summary>
    public bool IsPointerUnlocked => false;

    /// <summary>Outside the original's view window (UWClassicScreen.IsScreenPositionInGameArea).</summary>
    public bool IsPointerOverInterface(Vector2 pOAt)
    {
        UWGameUI lOUi = fUi();

        return lOUi != null && !lOUi.ClassicScreen.IsScreenPositionInGameArea(pOAt);
    }

    /// <summary>Outside the view window: no world raycast from the backpack or the paperdoll
    /// panel (UWClassicWorldGesture.Begin).</summary>
    public bool IsPointerOutsideView(Vector2 pOAt)
    {
        UWGameUI lOUi = fUi();

        return lOUi != null && !lOUi.ClassicScreen.IsScreenPositionInGameArea(pOAt);
    }

    /// <summary>The press in the view window (UWPlayerAttackInput.UpdatePress; without a UI
    /// anywhere).</summary>
    public bool PressAimsAtWorld(bool pbFromPad)
    {
        UWGameUI lOUi = fUi();

        return lOUi == null || lOUi.ClassicScreen.IsScreenPositionInGameArea(UWPointer.Position);
    }

    /// <summary>The pointer's third of the view window, and its column (UWPlayerAttackInput.fChooseAttackKind).</summary>
    public UWAttackKindSource AttackKindSource => UWAttackKindSource.PointerThirdsInView;

    /// <summary>
    /// Outside the view window you do not point into the world. The click on the
    /// backpack is evaluated by UWItemDrag (fTryResolveTargetSpellInInventory) - there, because
    /// it can also still have its usual effect there.
    ///
    /// IN THE VIEW WINDOW ONLY THE RIGHT BUTTON AIMS. With the left one you keep walking, and
    /// the waiting spell keeps waiting - this applies to target spells as well as
    /// projectile spells (per user on the original, 2026-09-10).
    ///
    /// THIS REPLACES THE ASSUMPTION OF 2026-09-05 that the left button fires the shot
    /// in addition to walking. The walking itself was already correct and stays as it
    /// is (see UWPlayerInput.IsCursorMovementInProgress).
    /// </summary>
    public bool TryGetSpellTargetPoint(bool pbRightPressed, bool pbLeftPressed, out Vector2 pOAt)
    {
        pOAt = UWPointer.Position;

        UWGameUI lOUi = fUi();

        if (lOUi != null && !lOUi.ClassicScreen.IsScreenPositionInGameArea(pOAt))
            return false;

        return pbRightPressed;
    }

    /// <summary>The classic scroll's answer lines (UWClassicConversationRenderer).</summary>
    public IUWConversationChoiceRows ConversationChoiceRows
    {
        get
        {
            UWConversationController lOController = UWScene.ConversationController;

            return lOController != null ? lOController.ClassicChoiceRows : null;
        }
    }

    /// <summary>The parchment pages with [MORE]; nothing scrolls with the stick.</summary>
    public Rect ConversationHistoryRect => default;

    /// <summary>The classic use mode is the caller's (UWClassicUseMode).</summary>
    public bool TryBeginUseFromWorld(UWObject pOItem)
    {
        return false;
    }
}
