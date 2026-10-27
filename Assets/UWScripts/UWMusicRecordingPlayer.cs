using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UWDataImport.Audio;

/// <summary>
/// Plays the player's recording of a piece (UWMusicRecordings) on an AudioSource of its own,
/// beside the synthesizers' mix (UWAudioMixer). Once, as the pieces play: when it ends, the theme
/// logic goes on as after a synthesized piece.
///
/// The file is read in the background (UnityWebRequestMultimedia) and kept compressed in memory,
/// decoded while it plays, so a theme change does not stall the frame and a long recording costs
/// little memory. While it loads it counts as playing, so the theme logic does not take the
/// silence for the end of the piece.
/// </summary>
public class UWMusicRecordingPlayer : MonoBehaviour
{
    private AudioSource mOSource;

    private Coroutine mOLoading;

    private string msLoadingPath;

    private AudioClip mOClip;

    /// <summary>Loading or playing a recording.</summary>
    public bool IsBusy => mOLoading != null || (mOSource != null && mOSource.isPlaying);

    /// <summary>The gain the music has now (UWAudioSettings.MusicGain), set every frame by the
    /// mixer.</summary>
    public float Volume
    {
        set
        {
            if (mOSource != null)
                mOSource.volume = Mathf.Clamp01(value);
        }
    }

    private void Awake()
    {
        mOSource = gameObject.AddComponent<AudioSource>();
        mOSource.playOnAwake = false;
        mOSource.loop = false;
        mOSource.spatialBlend = 0f;
    }

    /// <summary>Plays the recording in psPath from its start; whatever played before stops.
    /// pFFailed is called when the file cannot be read or holds nothing to play, so the caller can
    /// play the piece another way.</summary>
    public void Play(string psPath, System.Action pFFailed)
    {
        Stop();

        msLoadingPath = psPath;
        mOLoading = StartCoroutine(fLoadAndPlay(psPath, pFFailed));
    }

    public void Stop()
    {
        if (mOLoading != null)
        {
            StopCoroutine(mOLoading);
            mOLoading = null;
        }

        msLoadingPath = null;

        if (mOSource != null)
        {
            mOSource.Stop();
            mOSource.clip = null;
        }

        if (mOClip != null)
        {
            Destroy(mOClip);
            mOClip = null;
        }
    }

    private IEnumerator fLoadAndPlay(string psPath, System.Action pFFailed)
    {
        AudioType leType = psPath.EndsWith(".wav", System.StringComparison.OrdinalIgnoreCase) ? AudioType.WAV : AudioType.OGGVORBIS;
        string lsUri = new System.Uri(psPath).AbsoluteUri;

        using (UnityWebRequest lORequest = UnityWebRequestMultimedia.GetAudioClip(lsUri, leType))
        {
            ((DownloadHandlerAudioClip)lORequest.downloadHandler).compressed = true;

            yield return lORequest.SendWebRequest();

            // Stopped or replaced while it loaded.
            if (msLoadingPath != psPath)
                yield break;

            mOLoading = null;
            msLoadingPath = null;

            if (lORequest.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning("UWMusicRecordingPlayer: " + psPath + " not read: " + lORequest.error);
                pFFailed?.Invoke();
                yield break;
            }

            AudioClip lOClip = DownloadHandlerAudioClip.GetContent(lORequest);

            if (lOClip == null || lOClip.length <= 0f)
            {
                Debug.LogWarning("UWMusicRecordingPlayer: " + psPath + " holds no sound Unity can play.");

                if (lOClip != null)
                    Destroy(lOClip);

                pFFailed?.Invoke();
                yield break;
            }

            mOClip = lOClip;
            mOSource.clip = lOClip;
            mOSource.Play();
        }
    }

    private void OnDestroy()
    {
        Stop();
    }
}
