using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// THE POINTER'S FRAME (cleanup step 7.7B, 2026-10-25): runs UWPointer once a frame before every other
/// script (UWUpdateOrder.Pointer) - the capture, the move by the mouse's counts, the classic walk's
/// clamp - and in LateUpdate, after every script has moved or hidden the pointer and chosen its
/// picture, draws our pointer while captured: UWPointerCursor's picture at the frame's scale (the
/// pictures come scaled), its hotspot on the position, on a canvas of its own over everything
/// (UWUiSorting.Pointer, where the system pointer was drawn). Free, Unity draws the system pointer
/// and this picture stays hidden. Created once and kept across scene loads.
/// </summary>
[DefaultExecutionOrder(UWUpdateOrder.Pointer)]
public class UWPointerDriver : MonoBehaviour
{
    private static UWPointerDriver msInstance;

    private RawImage mOImage;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void fCreate()
    {
        if (msInstance != null)
            return;

        GameObject lOObject = new GameObject("UWPointer");
        DontDestroyOnLoad(lOObject);
        msInstance = lOObject.AddComponent<UWPointerDriver>();
    }

    private void Awake()
    {
        UWPointer.Start();
    }

    private void OnDestroy()
    {
        UWPointer.Stop();

        if (msInstance == this)
            msInstance = null;
    }

    private void Update()
    {
        UWPointer.Tick();

        // The pad's pointer, decided once a frame before anyone asks: whether it drives and has its
        // buttons, the PadPointer context; and the places that wait for a pad press as the frame
        // begins, before any of them acts on it (UWPadOwners) - cleanup step 7.8.
        UWGamepadPointer.DecideFrame();
        UWPadOwners.Tick();
    }

    private void LateUpdate()
    {
        if (!UWPointer.IsDrawn)
        {
            if (mOImage != null && mOImage.enabled)
                mOImage.enabled = false;

            return;
        }

        if (mOImage == null)
        {
            // One canvas unit per screen pixel (UWUiKit.CreateOverlayCanvas): the pictures come at the
            // size they are to be drawn.
            Canvas lOCanvas = UWUiKit.CreateOverlayCanvas(transform, "Pointer Canvas", UWUiSorting.Pointer);

            mOImage = UWUiKit.CreateRawImage(lOCanvas.transform, "Pointer", UWUiKit.BottomLeft, UWUiKit.BottomLeft);
        }

        Texture2D lOPicture = UWPointer.Picture;
        Vector2 lOHotspot = UWPointer.Hotspot;
        RectTransform lORect = mOImage.rectTransform;

        if (mOImage.texture != lOPicture)
            mOImage.texture = lOPicture;

        // The hotspot counts from the top-left corner (as Cursor.SetCursor takes it), the canvas from the
        // bottom-left: the picture hangs from the position as the system pointer did, on whole pixels
        // (the speed moves the position by fractions).
        Vector2 lOAt = UWPointer.Position;

        lORect.sizeDelta = new Vector2(lOPicture.width, lOPicture.height);
        lORect.anchoredPosition = new Vector2(Mathf.Round(lOAt.x - lOHotspot.x), Mathf.Round(lOAt.y - (lOPicture.height - lOHotspot.y)));

        if (!mOImage.enabled)
            mOImage.enabled = true;
    }
}
