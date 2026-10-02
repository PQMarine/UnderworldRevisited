using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;

public class DebugFunctions : MonoBehaviour
{
    private bool mbToggle;
    private GameObject mOPlayer, mOSpectatorCam;
    private UWLevelLoader mOLevelLoader;
    private UWLighting mOLighting;
    private UWControls mControls;

    private void Awake()
    {
        mControls = new UWControls();
        mControls.Enable();
    }

    private void OnDestroy()
    {
        mControls.Dispose();
    }

	// Use this for initialization
	void Start ()
    {
        mbToggle = false;
        mOPlayer = UWScene.Player;
        mOSpectatorCam = UWScene.SpectatorCamera;
        fSetSpectatorView(false);
	}

	// Update is called once per frame
	void Update ()
    {
        // UWLighting is only created by UWLevelLoader in its own Start() via
        // AddComponent - depending on script execution order it may not exist yet at this
        // script's own Start(), so pick it up here instead of looking for it
        // once in Start().
        if (mOLevelLoader == null || mOLighting == null)
        {
            mOLevelLoader = UWScene.LevelLoader;
            mOLighting = UWScene.Lighting;
        }

        if (mControls.Debug.TeleportDebug.WasPressedThisFrame())
        {
            mOPlayer.transform.position = new Vector3(2020, 212, 128);
        }

        // N and B are silent in the main menu and character creation (see UWScreenUi.IsScreenMenuOpen).
        if (mControls.Debug.ToggleSpectator.WasPressedThisFrame() && !UWScreenUi.IsScreenMenuOpen)
        {
            if (!mbToggle)
            {
                mbToggle = !mbToggle;

                mOSpectatorCam.transform.position = mOPlayer.transform.position;
                mOSpectatorCam.transform.localRotation = mOPlayer.transform.localRotation;
                mOPlayer.SetActive(false);
                mOSpectatorCam.SetActive(true);
                fSetSpectatorView(true);

                if (mOLighting != null && !mOLighting.IsBright)
                    mOLighting.ToggleDebugBrightness();
            }
            else
            {
                mbToggle = !mbToggle;
                mOPlayer.transform.position = mOSpectatorCam.transform.position;
                //mOPlayer.transform.localRotation = mOSpectatorCam.transform.localRotation;
                fSetSpectatorView(false);
                mOPlayer.SetActive(true);
            }
        }

        // Switch level directly to get to a spot quickly - only in
        // noclip mode, since the player does not move along with it.
        if (mbToggle && mOLevelLoader != null)
        {
            if (mControls.Debug.NextLevel.WasPressedThisFrame())
                fJumpLevel(1);
            else if (mControls.Debug.PrevLevel.WasPressedThisFrame())
                fJumpLevel(-1);
        }

        if (mControls.Debug.ToggleBrightness.WasPressedThisFrame() && mOLighting != null && !UWScreenUi.IsScreenMenuOpen)
            mOLighting.ToggleDebugBrightness();

        // The F6 switch between the lowest detail level and the last other one is gone since
        // 2026-09-26 (per user) - the Graphics menu of the bar and the options panel choose it.
	}

    /// <summary>What the spectator camera renders in noclip mode (its layers from the scene).
    /// </summary>
    private int miSpectatorMask = -1;

    private bool mbSpectatorMaskKnown;

    /// <summary>
    /// THE SPECTATOR CAMERA stays active outside noclip mode as well - the scene starts it
    /// active, FindGameObjectWithTag only finds it so, and it is the one camera covering the
    /// whole screen. But outside noclip it only CLEARS the screen black behind the game camera's
    /// small viewport and draws nothing (per user, 2026-09-26: while a window was dragged,
    /// Unity's default blue showed around the frame - this camera's clear colour - and it drew
    /// the whole level a second time every frame under the game camera).
    /// </summary>
    private void fSetSpectatorView(bool pbView)
    {
        Camera lOCamera = mOSpectatorCam != null ? mOSpectatorCam.GetComponent<Camera>() : null;

        if (lOCamera == null)
            return;

        if (!mbSpectatorMaskKnown)
        {
            miSpectatorMask = lOCamera.cullingMask;
            mbSpectatorMaskKnown = true;
        }

        lOCamera.clearFlags = CameraClearFlags.SolidColor;
        lOCamera.backgroundColor = Color.black;
        lOCamera.cullingMask = pbView ? miSpectatorMask : 0;
    }

    /// <summary>
    /// Turns on noclip mode and places the camera at a position.
    ///
    /// For jumping into a SOLID tile (see UWDebugTools): there the player would
    /// otherwise stand inside rock. If the mode is already on, only the camera is moved.
    /// </summary>
    public void EnterSpectator(Vector3 pOPosition)
    {
        if (mOSpectatorCam == null || mOPlayer == null)
            return;

        if (!mbToggle)
        {
            mbToggle = true;

            mOSpectatorCam.transform.localRotation = mOPlayer.transform.localRotation;
            mOPlayer.SetActive(false);
            mOSpectatorCam.SetActive(true);
            fSetSpectatorView(true);

            if (mOLighting != null && !mOLighting.IsBright)
                mOLighting.ToggleDebugBrightness();
        }

        mOSpectatorCam.transform.position = pOPosition;
    }

    /// <summary>
    /// Loads the next or previous level (debug) and moves the noclip camera along.
    ///
    /// The player torch is attached to the (now deactivated) player, so the noclip camera brings
    /// no light of its own - therefore place it at a raised spot above the map centre
    /// instead of the old position (now from another level), which could lie in solid
    /// geometry.
    /// </summary>
    private void fJumpLevel(int piDirection)
    {
        mOLevelLoader.DebugLoadLevel(mOLevelLoader.CurrentLevelIndex + piDirection);
        mOSpectatorCam.transform.position = new Vector3(32 * 64f, 200f, 32 * 64f);
    }

    void OnGUI()
    {

        //GUI.Label(new Rect(10, 120, 500, 100), "X = " + (int)transform.position.x + " Y= " + (int)transform.position.y + " Z= " + (int)transform.position.z);
    }
}
