using System;
using System.IO;
using UnityEngine;
using UWDataImport.UWData;

/// <summary>
/// THE TEST MARKS IN THE WORLD (UWTestMarks, per user 2026-11-15): test-focus.json beside settings.json is read when it
/// changes (once a second the file's time is compared), and every mark of the level the player is on stands as a
/// numbered badge on a pin over its square's floor - drawn on a canvas of its own, so it shows through walls, inside the
/// 3D view (the camera's rectangle; the classic scheme's view is a window in the HUD). A mark behind the player or out of
/// the view is held at the view's edge, so its direction stays readable. Only with the
/// debug keys on (UWUserSettings.DebugKeys), whether the debug panel is open or not; without the file nothing is drawn.
/// Owned by the debug overlay (UWDebugOverlay), which also lists the marks in its report.
/// </summary>
public sealed class UWTestMarkView
{
    /// <summary>How often the file's time is compared, in seconds.</summary>
    private const float PollSeconds = 1f;

    /// <summary>The badge: its height and the room around the number.</summary>
    private const float BadgeHeight = 22f;

    private const float BadgePad = 6f;

    private const int NumberSize = 16;

    private const int LabelSize = 11;

    /// <summary>The pin stands this high over the floor (world units; a tile is 64).</summary>
    private const float PinHeight = 40f;

    /// <summary>A badge held at the edge keeps this far from it (layout units).</summary>
    private const float EdgeInset = 24f;

    private static readonly Color BadgeGround = new Color(0.05f, 0.05f, 0.06f, 0.85f);

    private static readonly Color PinColour = new Color(1f, 0.78f, 0.25f, 0.9f);

    private readonly UWKitRows mOKit;

    private float mfNextPoll;

    private DateTime mOFileTime = DateTime.MinValue;

    private bool mbFileSeen;

    /// <summary>Why the file could not be read, else null.</summary>
    public static string Problem { get; private set; }

    public UWTestMarkView(Transform pOOwner)
    {
        mOKit = new UWKitRows(pOOwner, UWUiSorting.TestMarks) { FixedPalette = UWGuiTheme.Plain };
    }

    public void Dispose()
    {
        mOKit.Dispose();
    }

    // ------------------------------------------------- The file

    /// <summary>Reads test-focus.json when it appeared, changed or went away (UWTestMarks.Current).</summary>
    public void Poll()
    {
        if (Time.unscaledTime < mfNextPoll)
            return;

        mfNextPoll = Time.unscaledTime + PollSeconds;

        string lsPath;

        try
        {
            lsPath = UWUserFolder.Combine(UWTestMarks.FileName);
        }
        catch (Exception)
        {
            return;
        }

        if (!File.Exists(lsPath))
        {
            if (mbFileSeen)
            {
                mbFileSeen = false;
                mOFileTime = DateTime.MinValue;
                UWTestMarks.Current = null;
                Problem = null;
            }

            return;
        }

        DateTime lOTime = File.GetLastWriteTimeUtc(lsPath);

        if (mbFileSeen && lOTime == mOFileTime)
            return;

        mbFileSeen = true;
        mOFileTime = lOTime;

        try
        {
            UWTestMarks lOMarks = UWTestMarks.Parse(File.ReadAllText(lsPath), out string lsError);

            // A broken file keeps the marks as they were and says why (the report's Test section).
            if (lOMarks != null)
                UWTestMarks.Current = lOMarks;

            Problem = lsError;
        }
        catch (Exception lOError)
        {
            Problem = lOError.Message;
        }

        Debug.Log("[TestMarks] " + UWTestMarks.FileName + (Problem != null ? " could not be read: " + Problem
            : " read: test " + UWTestMarks.Current.Test + ", " + UWTestMarks.Current.Marks.Count + " marks"));
    }

    // ------------------------------------------------- The report

    /// <summary>The marks for the report's Test section: where each lies from the player in words.</summary>
    public static void Fill(UWDebugSnapshot pOSnapshot, UWLevelLoader pOLoader)
    {
        UWTestMarks lOMarks = UWTestMarks.Current;

        if (lOMarks == null && Problem == null)
            return;

        pOSnapshot.Test = lOMarks != null ? (lOMarks.Test + (string.IsNullOrEmpty(lOMarks.Title) ? string.Empty : ": " + lOMarks.Title)) : "?";
        pOSnapshot.TestProblem = Problem;

        foreach (UWTrapLog.Entry lOEntry in UWTrapLog.MarkedEntries)
            pOSnapshot.MarkedTraps.Add(lOEntry);

        if (lOMarks == null)
            return;

        bool lbUw2 = UWGameStartUw2.IsRunning;
        int liLevel = pOLoader != null ? pOLoader.CurrentLevelIndex + 1 : 0;
        UWLevel lOLevel = pOLoader != null ? pOLoader.CurrentLevel : null;
        UWTile lOHere = pOSnapshot.HasPlace && lOLevel != null ? lOLevel.GetTile(pOSnapshot.TileX, pOSnapshot.TileZ) : null;

        foreach (UWTestMarks.Mark lOMark in lOMarks.Marks)
        {
            bool lbHere = lOMark.IsOn(lbUw2, liLevel);
            string lsWhere;

            if (!lbHere)
                lsWhere = "on level " + lOMark.Level + (lOMark.Game != null ? " of " + lOMark.Game.ToUpperInvariant() : string.Empty);
            else if (!pOSnapshot.HasPlace)
                lsWhere = "on this level";
            else
            {
                UWTile lOThere = lOLevel != null ? lOLevel.GetTile(lOMark.X, lOMark.Y) : null;
                int liSteps = lOHere != null && lOThere != null ? (lOThere.FloorHeight - lOHere.FloorHeight) >> 4 : 0;

                lsWhere = UWTestMarks.Direction(pOSnapshot.TileX, pOSnapshot.TileZ, lOMark.X, lOMark.Y, liSteps);
            }

            pOSnapshot.TestMarks.Add(new UWDebugTestMark
            {
                Number = lOMark.Number,
                Label = lOMark.Label,
                Expect = lOMark.Expect,
                Where = lsWhere,
                IsHere = lbHere
            });
        }
    }

    // ------------------------------------------------- The badges

    private static UWKitRows.HelpLook fLook(int piSize, Color pOColour, bool pbBold)
    {
        return new UWKitRows.HelpLook { Size = piSize, Colour = pOColour, Anchor = TextAnchor.MiddleCenter, Bold = pbBold };
    }

    /// <summary>Draws this frame's badges; pbShown false (no camera, the debug keys off) puts the canvas out.</summary>
    public void Draw(bool pbShown, Camera pOCamera, UWLevelLoader pOLoader)
    {
        UWTestMarks lOMarks = UWTestMarks.Current;
        UWLevel lOLevel = pOLoader != null ? pOLoader.CurrentLevel : null;
        bool lbShown = pbShown && pOCamera != null && lOMarks != null && lOLevel != null && lOMarks.Marks.Count > 0;

        mOKit.Begin(lbShown, false, false, false);

        if (!lbShown)
            return;

        bool lbUw2 = UWGameStartUw2.IsRunning;
        int liLevel = pOLoader.CurrentLevelIndex + 1;
        float lfScale = UWGuiTheme.Scale;
        float lfWidth = mOKit.Width;
        float lfHeight = Screen.height / lfScale;

        foreach (UWTestMarks.Mark lOMark in lOMarks.Marks)
        {
            if (!lOMark.IsOn(lbUw2, liLevel))
                continue;

            UWTile lOTile = lOLevel.GetTile(lOMark.X, lOMark.Y);

            if (lOTile == null)
                continue;

            float lfFloor = UWUnits.ZPosToWorld(lOTile.FloorHeight >> 1);
            Vector3 lOFoot = new Vector3(UWUnits.TileCentreToWorldAxis(lOMark.X), lfFloor, UWUnits.TileCentreToWorldAxis(lOMark.Y));
            Vector3 lOTop = lOFoot + (Vector3.up * PinHeight);

            fDrawMark(lOMark, pOCamera, lOFoot, lOTop, lfScale, lfWidth, lfHeight);
        }

        mOKit.End();
    }

    private void fDrawMark(UWTestMarks.Mark pOMark, Camera pOCamera, Vector3 pOFoot, Vector3 pOTop, float pfScale, float pfWidth, float pfHeight)
    {
        Vector3 lOFoot = pOCamera.WorldToScreenPoint(pOFoot);
        Vector3 lOTop = pOCamera.WorldToScreenPoint(pOTop);
        bool lbInFront = lOFoot.z > 0f && lOTop.z > 0f;

        // Screen pixels (y up) to layout units (y down).
        Vector2 lOFootAt = new Vector2(lOFoot.x / pfScale, pfHeight - (lOFoot.y / pfScale));
        Vector2 lOTopAt = new Vector2(lOTop.x / pfScale, pfHeight - (lOTop.y / pfScale));

        string lsNumber = pOMark.Number.ToString(System.Globalization.CultureInfo.InvariantCulture);
        UWKitRows.HelpLook lONumberLook = fLook(NumberSize, UWGuiTheme.Plain.Accent, true);
        UWKitRows.HelpLook lOLabelLook = fLook(LabelSize, UWGuiTheme.Plain.Text, false);
        float lfNumberWidth = Mathf.Max(BadgeHeight, mOKit.HelpTextWidth(lsNumber, lONumberLook) + (2f * BadgePad));
        float lfLabelWidth = string.IsNullOrEmpty(pOMark.Label) ? 0f : mOKit.HelpTextWidth(pOMark.Label, lOLabelLook) + (2f * BadgePad);

        // THE 3D VIEW, not the screen (per user, 2026-11-15: in the classic scheme the marks right of the small view
        // stood over the HUD and the inventory): the camera's own rectangle on the screen, in layout units.
        Rect lOPixels = pOCamera.pixelRect;
        Rect lOView = new Rect(lOPixels.x / pfScale, pfHeight - (lOPixels.yMax / pfScale), lOPixels.width / pfScale, lOPixels.height / pfScale);

        bool lbInView = lbInFront && lOView.Contains(lOTopAt);
        Vector2 lOBadgeAt = lOTopAt;

        if (lbInView)
        {
            // The pin from the floor to the badge, cut at the view's lower edge.
            float lfPinTop = Mathf.Max(lOView.yMin, Mathf.Min(lOTopAt.y, lOFootAt.y));
            float lfPinBottom = Mathf.Min(lOView.yMax, Mathf.Max(lOTopAt.y, lOFootAt.y));

            if (lfPinBottom > lfPinTop && lOFootAt.x >= lOView.xMin && lOFootAt.x <= lOView.xMax)
            {
                mOKit.Fill(new Rect(lOFootAt.x - 1f, lfPinTop, 2f, lfPinBottom - lfPinTop), PinColour);

                if (lOView.Contains(lOFootAt))
                    mOKit.Fill(new Rect(lOFootAt.x - 4f, lOFootAt.y - 2f, 8f, 4f), PinColour);
            }
        }
        else
        {
            // Out of the view or behind: held at the view's edge in the mark's direction from its centre.
            Vector2 lOCentre = lOView.center;
            Vector2 lODirection = lOTopAt - lOCentre;

            if (!lbInFront)
                lODirection = -lODirection;

            if (lODirection.sqrMagnitude < 0.001f)
                lODirection = Vector2.down;

            float lfHalfWidth = Mathf.Max(1f, (lOView.width * 0.5f) - EdgeInset);
            float lfHalfHeight = Mathf.Max(1f, (lOView.height * 0.5f) - EdgeInset);
            float lfReach = Mathf.Min(lfHalfWidth / Mathf.Max(0.001f, Mathf.Abs(lODirection.x)), lfHalfHeight / Mathf.Max(0.001f, Mathf.Abs(lODirection.y)));

            lOBadgeAt = lOCentre + (lODirection * lfReach);
        }

        // The badge: the number on the accent, the label beside it, kept inside the view.
        float lfTotal = lfNumberWidth + lfLabelWidth;
        Rect lONumberRect = new Rect(lOBadgeAt.x - (lfNumberWidth * 0.5f), lOBadgeAt.y - BadgeHeight, lfNumberWidth, BadgeHeight);

        lONumberRect.x = Mathf.Clamp(lONumberRect.x, lOView.xMin, Mathf.Max(lOView.xMin, lOView.xMax - lfTotal));
        lONumberRect.y = Mathf.Clamp(lONumberRect.y, lOView.yMin, Mathf.Max(lOView.yMin, lOView.yMax - BadgeHeight));

        mOKit.Fill(new Rect(lONumberRect.x - 1f, lONumberRect.y - 1f, lfTotal + 2f, BadgeHeight + 2f), PinColour);
        mOKit.Fill(new Rect(lONumberRect.x, lONumberRect.y, lfTotal, BadgeHeight), BadgeGround);
        mOKit.HelpText(lONumberRect, lsNumber, lONumberLook);

        if (lfLabelWidth > 0f)
            mOKit.HelpText(new Rect(lONumberRect.xMax, lONumberRect.y, lfLabelWidth, BadgeHeight), pOMark.Label, lOLabelLook);
    }
}
