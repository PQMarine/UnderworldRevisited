using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

/// <summary>
/// THE CINEMATIC CAMERA (per user, 2026-11-13, for a video of the port): a debug tool that flies the view along points
/// set in the game, smoothly and at an even speed. Only with the debug keys on (UWUserSettings.DebugKeys), never in
/// the game's own logic.
///
/// KEYS, read off the keyboard (none of them is bound in the game's walk; skipped while text is typed):
///   Insert        the current view becomes the next point (the player's eye, or the noclip camera of N)
///   Delete        takes the last point away; Shift+Delete all of them
///   Home          plays the flight, or stops it
///   End           the speed: slow, normal, fast
///   Ctrl+Home     shows or hides the small window: the keys, the nine slots with their paths, buttons
///
/// NINE SLOTS (per user, 2026-11-13): each holds one path for one game and level; a click on a slot's row in the
/// window chooses it. A slot that holds a path of another game or level takes no point until it is cleared.
///
/// HOW IT FLIES: as a claim on UWRemoteCamera (the camera that Roaming Sight and the camera trap use), at the lowest
/// priority. The player's body, Camera.main and the creatures stay where they are, the light goes with the view, the
/// controls are held while it flies, and a level change ends it - all of that is UWRemoteCamera's own. Nothing of the
/// existing code is changed for it: this component creates itself after the scene is loaded and only reads the keys.
///
/// THE PATH is a centripetal Catmull-Rom curve through the points for the place and through yaw and pitch for the
/// look, run at an even speed along its length, easing in and out. The slots are kept in cinematic-path.json beside the
/// settings at every change, so the same flight can be filmed again in every graphics mode and another day.
/// </summary>
[DefaultExecutionOrder(-1000)]
public sealed class UWCinematicCamera : MonoBehaviour
{
    /// <summary>Below the spell (10) and the trap (20): either of them lies over a flight.</summary>
    private const int Priority = 5;

    private const string FileName = "cinematic-path.json";

    /// <summary>World units a second: slow, normal, fast (a tile is 64).</summary>
    private static readonly float[] msySpeeds = { 24f, 48f, 96f };

    /// <summary>The carried light's place beside the eye stretched by this (per user 2026-11-13: at the walking place the
    /// flight showed hardly any shadows): as when walking, twice, four times.</summary>
    private static readonly float[] msySpreads = { 1f, 2f, 4f };

    private static readonly string[] msySpreadNames = { "as walking", "wider", "far" };

    /// <summary>Seconds of easing in and out at the ends.</summary>
    private const float EaseSeconds = 1.2f;

    /// <summary>Samples per segment for the length table.</summary>
    private const int SamplesPerSegment = 64;

    [System.Serializable]
    private sealed class PathFile
    {
        public string Game;

        public int Level;

        public int Speed = 1;

        public List<Point> Points = new List<Point>();
    }

    public const int SlotCount = 9;

    [System.Serializable]
    private sealed class SlotsFile
    {
        public int Active;

        /// <summary>How far the carried light stands beside the flying eye: an index into msySpreads.</summary>
        public int Light = 1;

        public List<PathFile> Slots = new List<PathFile>();
    }

    [System.Serializable]
    private sealed class Point
    {
        public Vector3 Position;

        public float Yaw;

        public float Pitch;
    }

    private SlotsFile mOFile;

    /// <summary>The chosen slot's path.</summary>
    private PathFile mOPath => mOFile.Slots[mOFile.Active];

    private int miClaim;

    private bool mbFlying;

    private float mfTravelled;

    private float mfLength;

    private readonly List<float> mOLengths = new List<float>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void fCreate()
    {
        GameObject lOObject = new GameObject("UW cinematic camera");

        DontDestroyOnLoad(lOObject);
        lOObject.AddComponent<UWCinematicCamera>();
    }

    private void Update()
    {
        Keyboard lOKeys = Keyboard.current;

        UWMouseButtons.IsBlockedByCinematic = false;

        if (lOKeys == null || !UWUserSettings.DebugKeys || UWControls.IsTextEntryActive || UWScene.LevelLoader == null)
        {
            if (mbFlying)
                fStop(null);

            mbWindow = false;

            return;
        }

        fUpdateWindowBlock();

        if (lOKeys.homeKey.wasPressedThisFrame && lOKeys.ctrlKey.isPressed)
            mbWindow = !mbWindow;
        else if (lOKeys.homeKey.wasPressedThisFrame)
        {
            if (mbFlying)
                fStop("Cinematic camera: stopped.");
            else
                fStart();
        }
        else if (!mbFlying && lOKeys.insertKey.wasPressedThisFrame)
            fAddPoint();
        else if (!mbFlying && lOKeys.deleteKey.wasPressedThisFrame)
            fRemove(lOKeys.shiftKey.isPressed);
        else if (lOKeys.endKey.wasPressedThisFrame)
            fNextSpeed();

        if (mbFlying)
            fFly();
    }

    // ------------------------------------------------- Points

    private void fAddPoint()
    {
        Camera lOCamera = UWViewpoint.Current;

        if (lOCamera == null)
            return;

        fLoad();

        string lsGame = fGame();
        int liLevel = UWScene.LevelLoader.CurrentLevelIndex;

        // A slot with a path of another game or level keeps it: choose another slot or clear this one.
        if (mOPath.Points.Count > 0 && (mOPath.Game != lsGame || mOPath.Level != liLevel))
        {
            fSay("Cinematic camera: slot " + (mOFile.Active + 1) + " holds a path of " + mOPath.Game + " level " + (mOPath.Level + 1)
                + " - choose another slot or clear it (Shift+Delete).");

            return;
        }

        mOPath.Game = lsGame;
        mOPath.Level = liLevel;

        Vector3 lOAngles = lOCamera.transform.eulerAngles;

        mOPath.Points.Add(new Point { Position = lOCamera.transform.position, Yaw = lOAngles.y, Pitch = fSigned(lOAngles.x) });
        fSave();
        fSay("Cinematic camera: slot " + (mOFile.Active + 1) + ", point " + mOPath.Points.Count + " set.");
    }

    private void fRemove(bool pbAll)
    {
        fLoad();

        if (mOPath.Points.Count == 0)
            return;

        if (pbAll)
            mOPath.Points.Clear();
        else
            mOPath.Points.RemoveAt(mOPath.Points.Count - 1);

        fSave();
        fSay("Cinematic camera: slot " + (mOFile.Active + 1) + ", " + mOPath.Points.Count + " points left.");
    }

    private void fNextSpeed()
    {
        fLoad();
        mOPath.Speed = (mOPath.Speed + 1) % msySpeeds.Length;
        fSave();
        fSay("Cinematic camera: slot " + (mOFile.Active + 1) + ", speed " + fSpeedName(mOPath.Speed) + ".");
    }

    // ------------------------------------------------- Flight

    private void fStart()
    {
        fLoad();

        if (mOPath.Points.Count < 2)
        {
            fSay("Cinematic camera: set at least two points (Insert).");

            return;
        }

        if (mOPath.Game != fGame() || mOPath.Level != UWScene.LevelLoader.CurrentLevelIndex)
        {
            fSay("Cinematic camera: the path belongs to " + mOPath.Game + " level " + (mOPath.Level + 1) + ".");

            return;
        }

        UWRemoteCamera lORemote = UWRemoteCamera.Ensure();

        if (lORemote == null)
            return;

        fUnwrapYaw();
        fMeasure();

        Point lOFirst = mOPath.Points[0];

        mfTravelled = 0f;
        mbFlying = true;

        // A hidden interface (H) stays hidden while the view flies.
        UWInterfaceHide.HoldHidden = UWInterfaceHide.IsHidden;

        fBorrowMainCameraLook(lORemote);

        // Where the carried light stands from the walking eye, before the remote view takes it to its own eye.
        Camera lOMainEye = Camera.main;

        mOLightFromEye = UWScene.Lighting != null && lOMainEye != null ? UWScene.Lighting.CarriedLightFromEye(lOMainEye.transform) : Vector3.zero;

        // The carried light beside the flying eye, after the remote camera has put it at the eye (UWLighting.PlaceAtView).
        Application.onBeforeRender -= fPlaceLight;
        Application.onBeforeRender += fPlaceLight;
        miClaim = lORemote.Begin(Priority, lOFirst.Position, lOFirst.Yaw, lOFirst.Pitch, false, fOnClaimEnded);
    }

    /// <summary>The camera ended the claim itself (a level change, the scene torn down).</summary>
    private void fOnClaimEnded()
    {
        Application.onBeforeRender -= fPlaceLight;
        fReturnCameraLook();
        miClaim = 0;
        mbFlying = false;
        UWInterfaceHide.HoldHidden = false;
    }

    private void fStop(string psLine)
    {
        int liClaim = miClaim;

        mbFlying = false;
        miClaim = 0;
        UWInterfaceHide.HoldHidden = false;
        Application.onBeforeRender -= fPlaceLight;
        fReturnCameraLook();

        if (liClaim != 0 && UWRemoteCamera.Current != null)
            UWRemoteCamera.Current.End(liClaim);

        if (psLine != null)
            fSay(psLine);
    }

    private void fFly()
    {
        UWRemoteCamera lORemote = UWRemoteCamera.Current;

        if (lORemote == null || !lORemote.HasClaim(miClaim))
        {
            mbFlying = false;
            UWInterfaceHide.HoldHidden = false;

            return;
        }

        float lfSpeed = msySpeeds[Mathf.Clamp(mOPath.Speed, 0, msySpeeds.Length - 1)];
        float lfEase = Mathf.Min(EaseSeconds * lfSpeed, mfLength * 0.5f);
        float lfFactor = 1f;

        // Slow at both ends, never quite at rest (a tenth of the speed), so the flight always moves on.
        if (lfEase > 0f)
            lfFactor = Mathf.Clamp(Mathf.Min(mfTravelled, mfLength - mfTravelled) / lfEase, 0.1f, 1f);

        mfTravelled += lfSpeed * lfFactor * Time.unscaledDeltaTime;

        if (mfTravelled >= mfLength)
        {
            fPose(mfLength, lORemote);
            fStop("Cinematic camera: done.");

            return;
        }

        fPose(mfTravelled, lORemote);
    }

    private void fPose(float pfDistance, UWRemoteCamera pORemote)
    {
        fAt(pfDistance, out int liSegment, out float lfT);

        List<Point> lOPoints = mOPath.Points;
        Point lO0 = lOPoints[Mathf.Max(liSegment - 1, 0)];
        Point lO1 = lOPoints[liSegment];
        Point lO2 = lOPoints[liSegment + 1];
        Point lO3 = lOPoints[Mathf.Min(liSegment + 2, lOPoints.Count - 1)];

        Vector3 lOPosition = fCatmullRom(lO0.Position, lO1.Position, lO2.Position, lO3.Position, lfT);
        float lfYaw = fCatmullRom(lO0.Yaw, lO1.Yaw, lO2.Yaw, lO3.Yaw, lfT);
        float lfPitch = fCatmullRom(lO0.Pitch, lO1.Pitch, lO2.Pitch, lO3.Pitch, lfT);

        pORemote.SetPose(miClaim, lOPosition, lfYaw, lfPitch);
    }

    // ------------------------------------------------- The curve

    /// <summary>The cumulative length at every sample, for an even speed.</summary>
    private void fMeasure()
    {
        mOLengths.Clear();
        mOLengths.Add(0f);

        float lfTotal = 0f;
        List<Point> lOPoints = mOPath.Points;

        for (int liSegment = 0; liSegment < lOPoints.Count - 1; liSegment++)
        {
            Vector3 lOPrevious = lOPoints[liSegment].Position;
            Vector3 lO0 = lOPoints[Mathf.Max(liSegment - 1, 0)].Position;
            Vector3 lO3 = lOPoints[Mathf.Min(liSegment + 2, lOPoints.Count - 1)].Position;

            for (int liSample = 1; liSample <= SamplesPerSegment; liSample++)
            {
                Vector3 lONext = fCatmullRom(lO0, lOPoints[liSegment].Position, lOPoints[liSegment + 1].Position, lO3,
                    liSample / (float)SamplesPerSegment);

                lfTotal += Vector3.Distance(lOPrevious, lONext);
                mOLengths.Add(lfTotal);
                lOPrevious = lONext;
            }
        }

        // Points at one spot (a turn on the place): the turn still takes its time, a tile's worth of travel.
        mfLength = Mathf.Max(lfTotal, 1f);

        if (lfTotal < 1f)
        {
            for (int liAt = 0; liAt < mOLengths.Count; liAt++)
                mOLengths[liAt] = liAt / (float)(mOLengths.Count - 1) * mfLength;
        }
    }

    /// <summary>The segment and its parameter at a distance along the path.</summary>
    private void fAt(float pfDistance, out int piSegment, out float pfT)
    {
        int liLow = 0;
        int liHigh = mOLengths.Count - 1;

        while (liHigh - liLow > 1)
        {
            int liMid = (liLow + liHigh) / 2;

            if (mOLengths[liMid] < pfDistance)
                liLow = liMid;
            else
                liHigh = liMid;
        }

        float lfSpan = mOLengths[liHigh] - mOLengths[liLow];
        float lfSample = liLow + (lfSpan > 0f ? (pfDistance - mOLengths[liLow]) / lfSpan : 0f);

        piSegment = Mathf.Clamp((int)(lfSample / SamplesPerSegment), 0, mOPath.Points.Count - 2);
        pfT = Mathf.Clamp01((lfSample - (piSegment * SamplesPerSegment)) / SamplesPerSegment);
    }

    /// <summary>The yaws as one running angle, so a turn across north goes the short way round.</summary>
    private void fUnwrapYaw()
    {
        List<Point> lOPoints = mOPath.Points;

        for (int liAt = 1; liAt < lOPoints.Count; liAt++)
            lOPoints[liAt].Yaw = lOPoints[liAt - 1].Yaw + Mathf.DeltaAngle(lOPoints[liAt - 1].Yaw, lOPoints[liAt].Yaw);
    }

    private static Vector3 fCatmullRom(Vector3 pO0, Vector3 pO1, Vector3 pO2, Vector3 pO3, float pfT)
    {
        return new Vector3(fCatmullRom(pO0.x, pO1.x, pO2.x, pO3.x, pfT), fCatmullRom(pO0.y, pO1.y, pO2.y, pO3.y, pfT),
            fCatmullRom(pO0.z, pO1.z, pO2.z, pO3.z, pfT));
    }

    private static float fCatmullRom(float pf0, float pf1, float pf2, float pf3, float pfT)
    {
        float lfT2 = pfT * pfT;
        float lfT3 = lfT2 * pfT;

        return 0.5f * ((2f * pf1) + ((pf2 - pf0) * pfT) + (((2f * pf0) - (5f * pf1) + (4f * pf2) - pf3) * lfT2)
            + (((3f * pf1) - pf0 - (3f * pf2) + pf3) * lfT3));
    }

    private static float fSigned(float pfAngle)
    {
        return pfAngle > 180f ? pfAngle - 360f : pfAngle;
    }

    // ------------------------------------------------- The window (Ctrl+Home)

    private bool mbWindow;

    private UWKitRows mOKit;

    /// <summary>Where the window stood when last drawn, in screen pixels with y up, for the block of the game's buttons.</summary>
    private Rect mOWindowOnScreen;

    /// <summary>A press on the window keeps the game's buttons off until every button is up.</summary>
    private bool mbSwallowUntilReleased;

    private const float WindowWidth = 360f;

    private const float RowHeight = 20f;

    private const float Pad = 8f;

    private const float Margin = 12f;

    private const float ButtonGap = 4f;

    private static readonly Color Ground = new Color(0.04f, 0.04f, 0.05f, 0.78f);

    private static readonly Color ButtonGround = new Color(0.14f, 0.14f, 0.16f, 0.9f);

    private static readonly Color ButtonHover = new Color(0.24f, 0.24f, 0.28f, 0.95f);

    private static readonly Color ActiveGround = new Color(0.20f, 0.26f, 0.34f, 0.9f);

    // ------------------------------------------------- The flying camera's look

    private UniversalAdditionalCameraData mOBorrowed;

    /// <summary>The carried light from the walking eye, measured at the flight's start (UWLighting.CarriedLightFromEye).</summary>
    private Vector3 mOLightFromEye;

    private bool mbOwnPostProcessing;

    private AntialiasingMode meOwnAntialiasing;

    private bool mbOwnShadows;

    /// <summary>
    /// THE MAIN CAMERA'S LOOK FOR THE FLIGHT (per user 2026-11-13: no light setting made the flight look right): the
    /// remote camera keeps URP's defaults for post-processing and antialiasing, which the render path sets on it only
    /// while the world resolution or the gamma force post-processing (UWRenderPathHost.fApplyCameraFlags) - so in
    /// Remastered and Palette colours the flight lost the Volume (tonemapping, bloom, the colours' own passes). For the
    /// flight it takes the main camera's values, and gives its own back after it; Roaming Sight and the camera trap
    /// are untouched.
    /// </summary>
    private void fBorrowMainCameraLook(UWRemoteCamera pORemote)
    {
        Camera lOMain = Camera.main;
        UniversalAdditionalCameraData lOMainData = lOMain != null ? lOMain.GetComponent<UniversalAdditionalCameraData>() : null;
        UniversalAdditionalCameraData lORemoteData = pORemote != null && pORemote.ViewCamera != null
            ? pORemote.ViewCamera.GetComponent<UniversalAdditionalCameraData>() : null;

        if (lOMainData == null || lORemoteData == null)
            return;

        mOBorrowed = lORemoteData;
        mbOwnPostProcessing = lORemoteData.renderPostProcessing;
        meOwnAntialiasing = lORemoteData.antialiasing;
        mbOwnShadows = lORemoteData.renderShadows;

        lORemoteData.renderPostProcessing = lOMainData.renderPostProcessing;
        lORemoteData.antialiasing = lOMainData.antialiasing;
        lORemoteData.renderShadows = lOMainData.renderShadows;
    }

    private void fReturnCameraLook()
    {
        if (mOBorrowed == null)
            return;

        mOBorrowed.renderPostProcessing = mbOwnPostProcessing;
        mOBorrowed.antialiasing = meOwnAntialiasing;
        mOBorrowed.renderShadows = mbOwnShadows;
        mOBorrowed = null;
    }

    /// <summary>Before the frame renders: the light beside the flying eye (only while this flight's claim renders).</summary>
    private void fPlaceLight()
    {
        UWRemoteCamera lORemote = UWRemoteCamera.Current;

        if (!mbFlying || lORemote == null || !lORemote.IsClaimActive(miClaim) || UWScene.Lighting == null)
            return;

        // The spread stretches the sideways and forward part; the drop below the eye stays as when walking.
        float lfSpread = msySpreads[Mathf.Clamp(mOFile.Light, 0, msySpreads.Length - 1)];
        Vector3 lOFromEye = new Vector3(mOLightFromEye.x * lfSpread, mOLightFromEye.y, mOLightFromEye.z * lfSpread);

        UWScene.Lighting.PlaceAtView(lORemote.transform.position, lORemote.transform.eulerAngles.y, lOFromEye);
    }

    private void OnDestroy()
    {
        Application.onBeforeRender -= fPlaceLight;
        UWMouseButtons.IsBlockedByCinematic = false;

        if (mOKit != null)
            mOKit.Dispose();
    }

    /// <summary>The game's buttons stay off the window while the pointer is on it, and from a press on it until
    /// every button is up (as UWDebugOverlay's panel).</summary>
    private void fUpdateWindowBlock()
    {
        bool lbOver = mbWindow && UWPointer.HasMouse && mOWindowOnScreen.Contains(UWPointer.Position);
        bool lbPressed = UWPointer.HasMouse && (UWPointer.LeftPressed || UWPointer.RightPressed || UWPointer.MiddlePressed);
        bool lbHeld = UWPointer.HasMouse && (UWPointer.LeftHeld || UWPointer.RightHeld || UWPointer.MiddleHeld);

        if (lbOver && lbPressed)
            mbSwallowUntilReleased = true;
        else if (!lbHeld)
            mbSwallowUntilReleased = false;

        UWMouseButtons.IsBlockedByCinematic = lbOver || mbSwallowUntilReleased;
    }

    private void LateUpdate()
    {
        bool lbShown = mbWindow && UWUserSettings.DebugKeys && UWScene.LevelLoader != null;

        if (mOKit == null)
        {
            if (!lbShown)
                return;

            mOKit = new UWKitRows(transform, UWUiSorting.DebugPanel) { FixedPalette = UWGuiTheme.Plain };
        }

        mOKit.Begin(lbShown, false, false, false);

        if (!lbShown)
        {
            mOWindowOnScreen = default;

            return;
        }

        fLoad();

        UWKitRows.HelpLook lOText = fLook(UWGuiTheme.Plain.Text);
        UWKitRows.HelpLook lODim = fLook(UWGuiTheme.Plain.DimText);
        UWKitRows.HelpLook lOAccent = fLook(UWGuiTheme.Plain.Accent);
        float lfInner = WindowWidth - (2f * Pad);
        float lfHeight = Pad + (RowHeight * (2 + SlotCount + 1 + 3 + 6)) + Pad;
        Rect lOWindow = new Rect(mOKit.Width - WindowWidth - Margin, Margin, WindowWidth, lfHeight);
        float lfX = lOWindow.x + Pad;
        float lfY = lOWindow.y + Pad;

        mOKit.Fill(lOWindow, Ground);

        mOKit.HelpText(new Rect(lfX, lfY, lfInner, RowHeight), "Cinematic camera" + (mbFlying ? "  - flying" : string.Empty), lOAccent);
        lfY += RowHeight;
        mOKit.HelpText(new Rect(lfX, lfY, lfInner, RowHeight), "Now: " + fGame() + " level " + (UWScene.LevelLoader.CurrentLevelIndex + 1)
            + "   (a click on a slot chooses it)", lODim);
        lfY += RowHeight;

        // The slots, the chosen one lit; a click chooses one (not while flying).
        for (int liSlot = 0; liSlot < SlotCount; liSlot++)
        {
            PathFile lOSlot = mOFile.Slots[liSlot];
            string lsLine = (liSlot + 1) + "   " + (lOSlot.Points.Count == 0 ? "empty"
                : lOSlot.Game + " level " + (lOSlot.Level + 1) + ", " + lOSlot.Points.Count + " points, " + fSpeedName(lOSlot.Speed));
            bool lbActive = liSlot == mOFile.Active;

            if (mOKit.HelpButton(new Rect(lfX, lfY, lfInner, RowHeight - 2f), lsLine, lbActive ? lOAccent : lOText,
                lbActive ? ActiveGround : ButtonGround, ButtonHover, UWGuiTheme.Plain.Text) && !mbFlying)
            {
                mOFile.Active = liSlot;
                fSave();
            }

            lfY += RowHeight;
        }

        lfY += RowHeight * 0.5f;

        // The buttons, two rows.
        float lfButton = (lfInner - (2f * ButtonGap)) / 3f;

        if (fButton(lfX, lfY, lfButton, "Add point") && !mbFlying)
            fAddPoint();

        if (fButton(lfX + lfButton + ButtonGap, lfY, lfButton, "Remove last") && !mbFlying)
            fRemove(false);

        if (fButton(lfX + (2f * (lfButton + ButtonGap)), lfY, lfButton, "Clear slot") && !mbFlying)
            fRemove(true);

        lfY += RowHeight + ButtonGap;

        if (fButton(lfX, lfY, lfButton, mbFlying ? "Stop" : "Play"))
        {
            if (mbFlying)
                fStop("Cinematic camera: stopped.");
            else
                fStart();
        }

        if (fButton(lfX + lfButton + ButtonGap, lfY, lfButton, "Speed: " + fSpeedName(mOPath.Speed)))
            fNextSpeed();

        if (fButton(lfX + (2f * (lfButton + ButtonGap)), lfY, lfButton, "Close"))
            mbWindow = false;

        lfY += RowHeight + ButtonGap;

        // The light beside the flying eye, for the shadows (Palette colours, Remastered).
        if (fButton(lfX, lfY, lfInner, "Light: " + msySpreadNames[Mathf.Clamp(mOFile.Light, 0, msySpreadNames.Length - 1)]))
        {
            mOFile.Light = (Mathf.Clamp(mOFile.Light, 0, msySpreads.Length - 1) + 1) % msySpreads.Length;
            fSave();
        }

        lfY += RowHeight + (RowHeight * 0.5f);

        // The keys.
        string[] lyKeys =
        {
            "Insert       add the current view as a point",
            "Delete       remove the last point (Shift: clear the slot)",
            "Home         play or stop",
            "End          speed: slow, normal, fast",
            "Ctrl+Home    this window;  H hides the interface",
            "Saved at every change: cinematic-path.json"
        };

        foreach (string lsKey in lyKeys)
        {
            mOKit.HelpText(new Rect(lfX, lfY, lfInner, RowHeight), lsKey, lODim);
            lfY += RowHeight;
        }

        mOKit.End();

        float lfScale = UWGuiTheme.Scale;

        mOWindowOnScreen = new Rect(lOWindow.x * lfScale, Screen.height - (lOWindow.yMax * lfScale), lOWindow.width * lfScale,
            lOWindow.height * lfScale);
    }

    private bool fButton(float pfX, float pfY, float pfWidth, string psText)
    {
        return mOKit.HelpButton(new Rect(pfX, pfY, pfWidth, RowHeight), psText, fLook(UWGuiTheme.Plain.Text, TextAnchor.MiddleCenter),
            ButtonGround, ButtonHover, UWGuiTheme.Plain.Text);
    }

    private static UWKitRows.HelpLook fLook(Color pOColour, TextAnchor peAnchor = TextAnchor.MiddleLeft)
    {
        return new UWKitRows.HelpLook { Size = 13, Colour = pOColour, Anchor = peAnchor };
    }

    private static string fSpeedName(int piSpeed)
    {
        return piSpeed == 0 ? "slow" : piSpeed == 1 ? "normal" : "fast";
    }

    // ------------------------------------------------- File, game, line

    private static string fGame()
    {
        return UWGameStartUw2.IsRunning ? "UW2" : "UW1";
    }

    private void fLoad()
    {
        if (mOFile != null)
            return;

        mOFile = new SlotsFile();

        try
        {
            string lsFile = UWUserFolder.Combine(FileName);

            if (File.Exists(lsFile))
            {
                string lsText = File.ReadAllText(lsFile);
                SlotsFile lORead = JsonUtility.FromJson<SlotsFile>(lsText);

                if (lORead != null && lORead.Slots != null && lORead.Slots.Count > 0)
                    mOFile = lORead;
                else
                {
                    // The first version's file held one path: it becomes slot 1.
                    PathFile lOOld = JsonUtility.FromJson<PathFile>(lsText);

                    if (lOOld != null && lOOld.Points != null && lOOld.Points.Count > 0)
                        mOFile.Slots.Add(lOOld);
                }
            }
        }
        catch (System.Exception lOError)
        {
            Debug.LogWarning("[Cinematic] " + FileName + " could not be read: " + lOError.Message);
        }

        while (mOFile.Slots.Count < SlotCount)
            mOFile.Slots.Add(new PathFile());

        for (int liAt = 0; liAt < mOFile.Slots.Count; liAt++)
        {
            if (mOFile.Slots[liAt] == null)
                mOFile.Slots[liAt] = new PathFile();

            if (mOFile.Slots[liAt].Points == null)
                mOFile.Slots[liAt].Points = new List<Point>();
        }

        mOFile.Active = Mathf.Clamp(mOFile.Active, 0, SlotCount - 1);
    }

    private void fSave()
    {
        try
        {
            File.WriteAllText(UWUserFolder.Combine(FileName), JsonUtility.ToJson(mOFile, true));
        }
        catch (System.Exception lOError)
        {
            Debug.LogWarning("[Cinematic] " + FileName + " could not be written: " + lOError.Message);
        }
    }

    /// <summary>The line in the game's message scroll (debug text, English), and in the log.</summary>
    private static void fSay(string psLine)
    {
        Debug.Log("[Cinematic] " + psLine);

        // The second game's lines go to its own scroll (as the trap firings' do, UWTriggerSystem.fScrollUw2).
        if (UWGameStartUw2.IsRunning)
        {
            UWGameScreenUw2.Say(psLine);

            return;
        }

        UWWorld lOWorld = UWWorld.Of(UWScene.LevelLoader);

        if (lOWorld != null && lOWorld.Presentation != null)
            lOWorld.Presentation.AddMessage(psLine);
    }
}
