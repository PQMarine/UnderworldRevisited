using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;
using UWDataImport.UWData;

/// <summary>
/// The debug keys of the scene: noclip (N) with the level keys (Page Up/Down), the debug brightness
/// (F11), the test teleport (P), the shader globals dump (Shift+F12) and the forced creature cast
/// (Shift+F11), and the noclip entry the debug tools' jump uses (EnterSpectator). It draws nothing:
/// its empty OnGUI, left from a position label long commented out, went in cleanup 8c (2026-10-25).
/// </summary>
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

        // The keys are UWHotkeys' rows since cleanup step 7.5 (2026-10-25), in the order they were
        // read here: P, N, the level keys, F11, Shift+F12, Shift+F11.
        if (mOKeys == null)
            mOKeys = new DebugKeys(this);

        UWHotkeys.Route(UWHotkeySite.DebugKeys, mOKeys);

        // The F6 switch between the lowest detail level and the last other one is gone since
        // 2026-09-26 (per user) - the Graphics menu of the bar and the options panel choose it.
	}

    private DebugKeys mOKeys;

    /// <summary>The debug keys' gates and handlers (UWHotkeys), on this component's own controls.</summary>
    private sealed class DebugKeys : UWHotkeyHost
    {
        private readonly DebugFunctions mODebug;

        public DebugKeys(DebugFunctions pODebug)
        {
            mODebug = pODebug;
        }

        protected override UWControls Controls => mODebug.mControls;

        public override bool Gate(UWHotkeyGate peGate, UWHotkeyRow pORow)
        {
            switch (peGate)
            {
                // N and B are silent in the main menu and character creation (see UWScreenUi.IsScreenMenuOpen).
                case UWHotkeyGate.ScreenMenu: return UWScreenUi.IsScreenMenuOpen;

                // Switch level directly to get to a spot quickly - only in noclip mode, since the
                // player does not move along with it.
                case UWHotkeyGate.Noclip: return mODebug.mbToggle && mODebug.mOLevelLoader != null;
                case UWHotkeyGate.Lighting: return mODebug.mOLighting != null;
                default: return false;
            }
        }

        public override bool Run(UWHotkeyHandler peHandler)
        {
            switch (peHandler)
            {
                case UWHotkeyHandler.Teleport:
                    mODebug.mOPlayer.transform.position = new Vector3(2020, 212, 128);
                    return true;

                case UWHotkeyHandler.ToggleNoclip:
                    mODebug.fToggleSpectator();
                    return true;

                case UWHotkeyHandler.NextLevel:
                    mODebug.fJumpLevel(1);
                    return true;

                case UWHotkeyHandler.PreviousLevel:
                    mODebug.fJumpLevel(-1);
                    return true;

                case UWHotkeyHandler.ToggleBrightness:
                    mODebug.mOLighting.ToggleDebugBrightness();
                    return true;

                case UWHotkeyHandler.DumpShaderGlobals:
                    UWShaderGlobalsDump.Dump();
                    return true;

                // Shift+F11: the nearest creature with a spell casts its second one at once (its first
                // when the second slot is empty) - for the test of a creature's area spell, which the
                // original rolls with its caster strength out of 128 and only out of melee reach.
                case UWHotkeyHandler.ForceCast:
                    mODebug.fForceNearestCast();
                    return true;

                default:
                    return false;
            }
        }
    }

    /// <summary>N: into noclip or out of it.</summary>
    private void fToggleSpectator()
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

            // Back to the normal light with the end of noclip (per user, 2026-10-12);
            // until then the debug brightness stayed on after leaving.
            if (mOLighting != null && mOLighting.IsBright)
                mOLighting.ToggleDebugBrightness();
        }
    }

    private void fForceNearestCast()
    {
        if (mOPlayer == null)
            return;

        UWCritter lONearest = null;
        float lfNearest = float.MaxValue;

        // The creatures from the world's search (cleanup step 3.2, 2026-10-17; before, a scene search).
        List<UWCritter> lOCritters = new List<UWCritter>();

        if (UWScene.World != null)
            UWScene.World.FindCritters(UWTileArea.WholeLevel, UWCreatureFilter.Any, lOCritters);

        foreach (UWCritter lOCritter in lOCritters)
        {
            if (lOCritter == null || lOCritter.Brain == null)
                continue;

            float lfDistance = (lOCritter.transform.position - mOPlayer.transform.position).sqrMagnitude;

            if (lfDistance < lfNearest)
            {
                lfNearest = lfDistance;
                lONearest = lOCritter;
            }
        }

        if (lONearest == null)
        {
            Debug.Log("ForceCast: no creature");
            return;
        }

        bool lbStarted = lONearest.Brain.ForceSpell(2) || lONearest.Brain.ForceSpell(1);
        UWCritterRecord lORecord = lONearest.Record;

        Debug.Log(string.Format("ForceCast: {0} id {1} {2}; goal {3} anim {4} slot {5} frame {6} interval {7} tile {8}/{9} dist {10:F1}",
            lONearest.name, lORecord != null ? lORecord.ItemId : -1, lbStarted ? "casts" : "has no spell",
            lORecord != null ? lORecord.Goal : -1, lORecord != null ? lORecord.Animation : -1,
            lORecord != null ? lORecord.SpellSlot : -1, lORecord != null ? lORecord.Frame : -1,
            lORecord != null ? lORecord.Interval : -1, lORecord != null ? lORecord.TileX : -1,
            lORecord != null ? lORecord.TileY : -1, Mathf.Sqrt(lfNearest)));
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
}
