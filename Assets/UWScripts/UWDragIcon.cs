using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UWDataImport.UWData;

/// <summary>
/// THE CLASSIC DRAG'S POINTER PICTURE (cleanup step 5.15, 2026-10-22: out of UWItemDrag): the
/// thing's icon that hangs on the pointer while it is dragged or held in the use mode, and the
/// hiding of the system pointer behind it. This class is the one owner of both for the drag and
/// the use mode; until then UWItemDrag wrote Cursor.visible in six places.
///
/// The other owners of Cursor.visible keep theirs, each for its own reason: UWCountPrompt, the
/// yes/no question and the mantra of UWPromptController, UWHudMap's note, UWHudOptions,
/// UWIntroPlayer, UWControlScheme's holds, the modern bags' thing on the free pointer
/// (UWPointerCursor.ShowSystemPointerForBags since cleanup 5.17; UWModernBags' own until then)
/// and the held pointer of the classic walk by the mouse (UWPointerCursor.fApplyHeldPointer,
/// 2026-10-23), which yields to the thing on the pointer.
///
/// NO PER-FRAME WRITE: UWControlScheme applies its holds only when they change (2026-10-22), so
/// nothing shows the pointer again each frame; the one repeated call here is the use mode's
/// KeepSystemPointerHidden, which writes only when the pointer did come back.
/// </summary>
public sealed class UWDragIcon
{
    // Must match UWGameUI.backpackSlotSize - in the original the icon is
    // exactly as large when dragging as in the backpack, no separate scale.
    private const float DragIconSize = 14f;

    private readonly UWGameUI mOGameUi;

    private readonly GameObject mOCanvas;

    private readonly Image mOIcon;

    /// <summary>
    /// Separate overlay canvas, same CanvasScaler as UWGameUI (ScaleWithScreenSize,
    /// 320x200 reference resolution) instead of fixed screen pixels - otherwise the icon was tiny
    /// compared to the rest of the UI (which is scaled up to the actual
    /// resolution via the same scaler). RectTransform.position = Mouse.current.position
    /// still stays correct: the scaler only affects how sizes given in reference units
    /// are converted to pixels, not the world-position-to-pixel
    /// mapping of a screen space overlay canvas.
    /// </summary>
    public UWDragIcon(UWGameUI pOGameUi)
    {
        mOGameUi = pOGameUi;

        // AT THE ROOT, NOT UNDER THE CAMERA (2026-10-06, per user with a screenshot: with a thing on
        // the pointer every turn of the view - pointer or keys - put the thing beside the pointer
        // in the turn's direction, the faster the turn the farther). UWItemDrag sits on the
        // camera; a canvas under it is turned with the player in the same frame, before Unity
        // drives it back to its screen pose, and a world position set from the mouse in that
        // moment is converted under the turned pose and stays wrong once the pose is restored.
        // An overlay canvas at the root is never moved by anyone; Destroy takes it down.
        // The same fitting as the frame, so the icon keeps its size in a narrow window (UWUiFit).
        mOCanvas = UWUiKit.CreateClassicCanvas(null, "Item Drag Canvas", UWUiSorting.ItemDrag).gameObject;

        mOIcon = UWUiKit.CreateImage(mOCanvas.transform, "DragIcon", UWUiKit.Centre, UWUiKit.Centre, Color.white);
        mOIcon.rectTransform.sizeDelta = new Vector2(DragIconSize, DragIconSize);
        mOIcon.enabled = false;
    }

    /// <summary>The drag canvas lives at the root and goes with UWItemDrag.</summary>
    public void Destroy()
    {
        if (mOCanvas != null)
            Object.Destroy(mOCanvas);
    }

    /// <summary>
    /// The thing goes onto the pointer - a drag begins, or the use mode. The normal pointer
    /// (movement direction arrows, see UWGameUI.Update) is replaced by the icon while dragging
    /// instead of showing both at once - UWGameUI only sets the cursor when Cursor.visible is
    /// true, and leaves it alone when false.
    /// </summary>
    public void Hold(UWObject pOItem)
    {
        fShow(pOItem);
        Cursor.visible = false;
    }

    /// <summary>Another thing on the pointer, the system pointer stays as it is - a swap.</summary>
    public void Change(UWObject pOItem)
    {
        fShow(pOItem);
    }

    /// <summary>Nothing on the pointer any more: the icon goes, the system pointer comes back.</summary>
    public void Release()
    {
        mOIcon.enabled = false;
        Cursor.visible = true;
    }

    /// <summary>The icon onto this position (the pointer's, UWItemDrag.Update).</summary>
    public void MoveTo(Vector2 pOPosition)
    {
        mOIcon.rectTransform.position = pOPosition;
    }

    /// <summary>The use mode's re-hide (UWClassicUseMode.Update): hides the system pointer again if
    /// it came back while the thing is the pointer and pbMayHide - nothing else asks for it then.</summary>
    public void KeepSystemPointerHidden(bool pbMayHide)
    {
        if (Cursor.visible && pbMayHide)
            Cursor.visible = false;
    }

    /// <summary>
    /// THE ICON FOLLOWS THE POINTER AFTER EVERYONE ELSE HAS MOVED IT (2026-10-06, per user: with a
    /// thing on the pointer and a turn under way, it drifted ahead of the pointer in the turn's
    /// direction). During a cursor movement UWGameUI.Update warps the pointer back onto the view
    /// window's edge every frame; Update here may run before that warp and so put the icon where
    /// the mouse had been pushed to, beyond the edge. LateUpdate runs after every Update, so the
    /// icon ends the frame on the pointer the frame is drawn with. UWItemDrag.LateUpdate calls it.
    /// </summary>
    public void Follow()
    {
        if (mOIcon == null || !mOIcon.enabled || Mouse.current == null)
            return;

        mOIcon.rectTransform.position = PointerPosition(mOGameUi);
    }

    /// <summary>The position the pointer is drawn at: UWPointerZones' clamped one during a cursor
    /// movement (after WarpCursorPosition the Input System keeps reporting the pushed-out
    /// position), else the mouse's.</summary>
    public static Vector2 PointerPosition(UWGameUI pOGameUi)
    {
        Vector2 lOMouse = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;

        if (pOGameUi != null && !UWControlScheme.IsModern
            && UWScene.PlayerMovement != null && UWScene.PlayerMovement.Input.IsCursorMovementInProgress)
            return pOGameUi.PointerZones.PointerPosition;

        return lOMouse;
    }

    private void fShow(UWObject pOItem)
    {
        UWTexture lOSource = pOItem?.Icon ?? pOItem?.Texture;

        if (lOSource == null)
        {
            mOIcon.enabled = false;
            return;
        }

        UWIconPalette.Apply(mOIcon);

        // Shared (UWIconCache, since 2026-10-19); the sprite of every drag used to stay behind.
        mOIcon.sprite = UWIconCache.Sprite(lOSource, FilterMode.Point);

        // AT THE POINTER AT ONCE: the position used to follow only in the next update, so for
        // one frame the icon stood where the last drag or use had ended (per user, 2026-09-26).
        if (Mouse.current != null)
            mOIcon.rectTransform.position = PointerPosition(mOGameUi);

        mOIcon.enabled = true;
        mOIcon.rectTransform.localScale = new Vector3(UWClassicScreen.HorizontalPixelFactor, 1f, 1f);
    }
}
