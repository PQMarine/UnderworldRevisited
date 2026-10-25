using UnityEngine;

/// <summary>
/// Makes sure there is an AudioListener. Without one Unity is completely silent, without any
/// error message - and that is exactly what happened here: the scene contained a single
/// listener, on the SpectatorCamera, and it was disabled. Sound had never been set up in the
/// project, because until then there was nothing to play.
///
/// The disabled listener is left untouched - it belongs to a camera that is deliberately off.
/// Instead the active camera gets its own. This is logged, because a component attaching itself
/// to other scene objects should not happen silently.
///
/// Cleanup step 6.6 (2026-10-24): the one home of what UWAudioEngine and UWSoundPlayer each did
/// in their own fEnsureListener (UWNoclipCamera keeps a single listener of its own).
/// </summary>
public static class UWAudioListenerGuard
{
    /// <summary>Adds a listener to the main camera when no active one exists, else to
    /// pOFallback (null: none - the sound engine's case, which has no object of its own to
    /// listen from).</summary>
    public static void Ensure(GameObject pOFallback)
    {
        foreach (AudioListener lOExisting in Object.FindObjectsByType<AudioListener>())
        {
            if (lOExisting.enabled && lOExisting.gameObject.activeInHierarchy)
                return;
        }

        Camera lOCamera = Camera.main;
        GameObject lOTarget = lOCamera != null ? lOCamera.gameObject : pOFallback;

        if (lOTarget == null)
            return;

        lOTarget.AddComponent<AudioListener>();

        Debug.Log("UWAudioListenerGuard: no active AudioListener found - attached one to '"
            + lOTarget.name + "', otherwise everything stays silent.");
    }
}
