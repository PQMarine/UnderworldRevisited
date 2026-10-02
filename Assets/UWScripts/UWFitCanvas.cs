using UnityEngine;
using UnityEngine.UI;

/// <summary>Keeps a classic 320x200 canvas fitted as UWUiFit says, every frame - the window
/// can be dragged and the 4:3 proportion blends while the help window opens. Runs before the
/// scaler reads its settings.</summary>
[DefaultExecutionOrder(-200)]
[RequireComponent(typeof(CanvasScaler))]
public class UWFitCanvas : MonoBehaviour
{
    private CanvasScaler mOScaler;

    private void Awake()
    {
        mOScaler = GetComponent<CanvasScaler>();
        UWUiFit.Apply(mOScaler);
    }

    private void Update()
    {
        UWUiFit.Apply(mOScaler);
    }
}
