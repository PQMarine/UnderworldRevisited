using System.Collections.Generic;
using UnityEngine;
using UWDataImport;
using UWDataImport.UWData;

/// <summary>
/// Plays the original SPEECH RECORDINGS. The VOC files are converted into
/// AudioClips on first access and kept.
///
/// Caution, common misunderstanding: these are not sound effects, but the 42
/// spoken lines of the intro (see UWSound). Ultima Underworld 1 has no
/// sampled effects at all - those are produced via MIDI. So this class can voice the
/// intro, but not a creaking door.
///
/// Playback runs through a dedicated AudioSource. Positional recordings use
/// AudioSource.PlayClipAtPoint so that several can sound at once - this requires
/// an AudioListener in the scene - which was missing here and is created when needed,
/// see fEnsureListener.
/// </summary>
public class UWSoundPlayer : MonoBehaviour
{
    [Header("Sound")]
    [SerializeField]
    [Tooltip("Volume, 0 to 1.")]
    [Range(0f, 1f)]
    private float mfVolume = 1f;

    [SerializeField]
    [Tooltip("Number of a speech recording without extension, e.g. 00 or 23 - for the " +
        "context menu test of this component in Play mode.")]
    private string msTestSoundName = "00";

    private DataImport mOData;

    private AudioSource mOSource;

    private bool mbListenerChecked;

    private readonly Dictionary<string, AudioClip> mOClips = new Dictionary<string, AudioClip>();

    public float Volume
    {
        get { return mfVolume; }
        set { mfVolume = Mathf.Clamp01(value); }
    }

    /// <summary>Whether the SOUND folder exists at all. If it is missing (with an
    /// incomplete copy of the game data it is often only on the CD image), this
    /// class simply does nothing instead of throwing errors.</summary>
    public bool IsAvailable
    {
        get
        {
            fEnsureData();

            return mOData != null && mOData.Sound != null && mOData.Sound.IsAvailable;
        }
    }

    /// <summary>All available speech recordings, without extension.</summary>
    public List<string> ListSpeechClips()
    {
        return IsAvailable ? mOData.Sound.ListSpeechClips() : new List<string>();
    }

    /// <summary>Converts a recording into an AudioClip. The result is kept; null
    /// if the file does not exist or cannot be read.</summary>
    public AudioClip GetClip(string psName)
    {
        if (string.IsNullOrEmpty(psName) || !IsAvailable)
            return null;

        if (mOClips.TryGetValue(psName, out AudioClip lOCached))
            return lOCached;

        UWVoc lOVoc = mOData.Sound.GetSpeech(psName);
        AudioClip lOClip = null;

        if (lOVoc != null && lOVoc.IsLoaded)
        {
            lOClip = AudioClip.Create("UW_" + psName, lOVoc.Samples.Length / lOVoc.Channels,
                lOVoc.Channels, lOVoc.SampleRate, false);
            lOClip.SetData(lOVoc.Samples, 0);
        }

        mOClips[psName] = lOClip;

        return lOClip;
    }

    /// <summary>Plays a recording without a position - for speech accompanying subtitles
    /// and user interface feedback.</summary>
    public bool Play(string psName)
    {
        AudioClip lOClip = GetClip(psName);

        if (lOClip == null)
            return false;

        fEnsureSource();
        mOSource.PlayOneShot(lOClip, mfVolume);

        return true;
    }

    /// <summary>Stops a running recording - for clicking onwards in a
    /// cutscene (see UWIntroPlayer).</summary>
    public void StopSpeech()
    {
        if (mOSource != null)
            mOSource.Stop();
    }

    /// <summary>Plays a recording at a world position. Several can
    /// overlap.</summary>
    public bool PlayAt(string psName, Vector3 pOWorldPosition)
    {
        AudioClip lOClip = GetClip(psName);

        if (lOClip == null)
            return false;

        fEnsureListener();
        AudioSource.PlayClipAtPoint(lOClip, pOWorldPosition, mfVolume);

        return true;
    }

    /// <summary>
    /// Makes sure there is an AudioListener. Without one Unity is completely
    /// silent, without any error message - and that is exactly what happened here: the scene
    /// contained a single listener, on the SpectatorCamera, and it was disabled. Sound had
    /// never been set up in the project, because until now there was nothing to play.
    ///
    /// The disabled listener is left untouched - it belongs to a camera that is
    /// deliberately off. Instead the active camera gets its own. This is
    /// logged, because a component attaching itself to other scene objects should not
    /// happen silently.
    /// </summary>
    private void fEnsureListener()
    {
        if (mbListenerChecked)
            return;

        mbListenerChecked = true;

        foreach (AudioListener lOExisting in Object.FindObjectsByType<AudioListener>())
        {
            if (lOExisting.enabled && lOExisting.gameObject.activeInHierarchy)
                return;
        }

        Camera lOCamera = Camera.main;
        GameObject lOTarget = lOCamera != null ? lOCamera.gameObject : gameObject;

        lOTarget.AddComponent<AudioListener>();

        Debug.Log("UWSoundPlayer: no active AudioListener found - attached one to '"
            + lOTarget.name + "', otherwise everything stays silent.");
    }

    private void fEnsureSource()
    {
        fEnsureListener();

        if (mOSource != null)
            return;

        mOSource = GetComponent<AudioSource>();

        if (mOSource == null)
            mOSource = gameObject.AddComponent<AudioSource>();

        // Play without position - spatial effects go through PlayClipAtPoint.
        mOSource.spatialBlend = 0f;
        mOSource.playOnAwake = false;
    }

    private void fEnsureData()
    {
        if (mOData != null)
            return;

        // The data source sits on the level loader and is looked up when needed, instead of
        // having to wire it up in the scene.
        UWLevelLoader lOLoader = UWScene.LevelLoader;

        if (lOLoader != null)
            mOData = lOLoader.UWDataImporter;
    }

    [ContextMenu("Play test sound")]
    private void fPlayTest()
    {
        if (!Play(msTestSoundName))
            Debug.LogWarning("UWSoundPlayer: recording '" + msTestSoundName + "' cannot be played.");
    }

    /// <summary>
    /// Reports everything that can stand between file and speaker. Meant for the case
    /// "there is no sound, but there is no error message either" - because Unity stays
    /// completely silent in that case.
    ///
    /// The most common cause is not in the code but in the UI: the
    /// "Mute Audio" toggle in the toolbar of the Game window. It survives restarts and
    /// mutes sound ONLY in the editor, while the operating system keeps playing sound.
    /// </summary>
    [ContextMenu("Check audio output")]
    private void fDiagnose()
    {
        System.Text.StringBuilder lOOut = new System.Text.StringBuilder();

        lOOut.AppendLine("UWSoundPlayer diagnosis:");
        lOOut.AppendLine("  Game data found: " + (mOData != null || IsAvailable));
        lOOut.AppendLine("  SOUND folder available: " + IsAvailable);

        AudioClip lOClip = GetClip(msTestSoundName);

        lOOut.AppendLine("  Test recording '" + msTestSoundName + "': "
            + (lOClip == null ? "NOT CREATED" : lOClip.length.ToString("0.00") + " s, "
                + lOClip.frequency + " Hz, " + lOClip.channels + " channel(s)"));

        fEnsureListener();

        int liListeners = 0;
        int liActiveListeners = 0;

        foreach (AudioListener lOListener in Object.FindObjectsByType<AudioListener>())
        {
            liListeners++;

            if (lOListener.enabled && lOListener.gameObject.activeInHierarchy)
                liActiveListeners++;
        }

        lOOut.AppendLine("  AudioListener: " + liListeners + " present, " + liActiveListeners + " active");
        lOOut.AppendLine("  AudioListener.volume: " + AudioListener.volume);
        lOOut.AppendLine("  AudioListener.pause: " + AudioListener.pause);
        lOOut.AppendLine("  own volume: " + mfVolume);

        fEnsureSource();

        lOOut.AppendLine("  AudioSource: volume " + mOSource.volume + ", mute " + mOSource.mute
            + ", spatialBlend " + mOSource.spatialBlend + ", active " + mOSource.isActiveAndEnabled);
        lOOut.AppendLine("  Time.timeScale: " + Time.timeScale);

        lOOut.AppendLine("  If everything looks fine here and there is still nothing to hear: "
            + "check 'Mute Audio' in the toolbar of the Game window.");

        Debug.Log(lOOut.ToString());
    }
}
