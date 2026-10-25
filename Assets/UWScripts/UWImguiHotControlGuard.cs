using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// IMGUI'S HOT CONTROL IS RELEASED WHEN NO MOUSE BUTTON IS HELD (per user, 2026-10-12, Linux VM:
/// after changing the resolution in the Display dialog the menu bar outside the game and the
/// modern in-game menu showed the hover but ignored every click). Every IMGUI button takes the
/// mouse with GUIUtility.hotControl on its MouseDown and gives it back on the MouseUp; a press
/// that loses its MouseUp - the window resized by Screen.SetResolution in the middle of the
/// click, as X11 does - leaves hotControl set, and from then on no other IMGUI control takes a
/// click. hotControl set while no button is down is never a legitimate state (text fields use
/// keyboardControl, sliders hold hotControl only while dragged), so it is cleared here and the
/// event is logged, to confirm the cause in the Player.log.
/// Created once and kept across scene loads.
/// </summary>
public class UWImguiHotControlGuard : MonoBehaviour
{
    private static UWImguiHotControlGuard msInstance;

    /// <summary>Frames a stuck hot control is tolerated before it is cleared - a click's
    /// MouseDown and MouseUp may lie a frame apart from the Input System's button state.</summary>
    private const int ToleratedFrames = 2;

    private int miStuckFrames;

    private int miLastScreenWidth;

    private int miLastScreenHeight;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void fCreate()
    {
        if (msInstance != null)
            return;

        GameObject lOObject = new GameObject("UW IMGUI Hot Control Guard");

        DontDestroyOnLoad(lOObject);
        msInstance = lOObject.AddComponent<UWImguiHotControlGuard>();
    }

    private void Update()
    {
        // The screen size as Unity reports it, logged on every change: whether it follows the
        // window after SetResolution is the second suspect of the Linux report.
        if (Screen.width != miLastScreenWidth || Screen.height != miLastScreenHeight)
        {
            if (miLastScreenWidth == 0)
                Debug.Log(string.Format("UWImguiHotControlGuard: start, screen {0}x{1}, rendering {2}x{3}, system {4}x{5}, current resolution {6}, mode {7}",
                    Screen.width, Screen.height, Display.main.renderingWidth, Display.main.renderingHeight,
                    Display.main.systemWidth, Display.main.systemHeight, Screen.currentResolution, Screen.fullScreenMode));
            else
                Debug.Log(string.Format("UWImguiHotControlGuard: screen {0}x{1} -> {2}x{3}, mode {4}",
                    miLastScreenWidth, miLastScreenHeight, Screen.width, Screen.height, Screen.fullScreenMode));

            miLastScreenWidth = Screen.width;
            miLastScreenHeight = Screen.height;
        }

        if (GUIUtility.hotControl == 0 || fAnyMouseButtonHeld())
        {
            miStuckFrames = 0;
            return;
        }

        if (++miStuckFrames <= ToleratedFrames)
            return;

        Debug.Log(string.Format("UWImguiHotControlGuard: hot control {0} held without a mouse button, released",
            GUIUtility.hotControl));

        GUIUtility.hotControl = 0;
        miStuckFrames = 0;
    }

    private static bool fAnyMouseButtonHeld()
    {
        return UWPointer.HasMouse && (UWPointer.LeftHeld || UWPointer.RightHeld
            || UWPointer.MiddleHeld);
    }

    private void OnDestroy()
    {
        if (msInstance == this)
            msInstance = null;
    }
}
