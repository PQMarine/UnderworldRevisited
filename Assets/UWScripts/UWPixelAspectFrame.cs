using UnityEngine;

/// <summary>
/// Keeps a 320x200 UI frame at the pixel proportion of the classic screen (UWDisplayAspect): squeezed
/// to 5/6 of its width when the classic screen is shown at 4:3, square pixels otherwise. For the
/// canvases outside the main GameFrame (conversation, menus), which the switch did not reach at
/// first (per user with a screenshot of the conversation, 2026-09-17). Clicks still land right,
/// because they are converted with ScreenPointToLocalPointInRectangle, which takes the scale
/// into account.
/// </summary>
public class UWPixelAspectFrame : MonoBehaviour
{
    private void LateUpdate()
    {
        float lfFactor = UWClassicScreen.HorizontalPixelFactor;

        if (!Mathf.Approximately(transform.localScale.x, lfFactor))
            transform.localScale = new Vector3(lfFactor, 1f, 1f);
    }
}
