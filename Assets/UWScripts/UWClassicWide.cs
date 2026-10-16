using System.Collections.Generic;
using UnityEngine;
using UWDataImport.UWData;

/// <summary>
/// THE CLASSIC SCHEME WIDE (per user, 2026-10-10: "the classic scheme always at 4:3, the checkbox
/// for widescreen"): the classic frame (UWGameUI.mGameFrame, 320 x 200 in its own units) widened
/// to the window's width by the cuts of Classic Wide (UWHudArt.BuildClassicWideFrame,
/// ClassicWideShiftX) - the frame picture grown, its hole wider, every part on it moved with its
/// band, the message box longer.
///
///   - EXTRA, the columns put in, follows the window (the frame's height fills it, a column 5/6
///     of a row, the classic scheme's 4:3); it is 0 while the setting is off, in the modern
///     scheme, while the conversation frame (CONV.BYT) or the big map show, and while the help
///     window squeezes the frame - those keep the original's 320.
///   - THE PARTS: the frame's direct children are moved once a frame, just before the canvases
///     are drawn, by their own place's band (x of their anchored position, the row from its y);
///     a part placed anew since keeps its new place as its original one. The view's contents (the
///     weapon, the window picture) move with the hole's middle (InHole), the message box grows
///     (Widened).
///   - The rest reads Extra: the frame's width (UWUiFit.FrameWidth, UWGameUI), the camera's area,
///     the original's point of a click (OriginalX).
/// </summary>
public static class UWClassicWide
{
    /// <summary>The columns the classic frame is wider than 320, this frame.</summary>
    public static int Extra { get; private set; }

    public static int Left => Extra / 2;

    private static RectTransform msFrame;

    private static readonly HashSet<RectTransform> msInHole = new HashSet<RectTransform>();

    private static readonly HashSet<RectTransform> msWidened = new HashSet<RectTransform>();

    /// <summary>Nodes anchored to the frame's corners whose place lies in the original's frame (the
    /// panel's pivot, UWHudPanel; the container window, UWHudInventory): they keep the original's
    /// size, moved by the band of the place named; their own offsets at registration.</summary>
    private struct FrameNode
    {
        public Vector2Int Place;
        public float BaseMin;
        public float BaseMax;
    }

    private static readonly Dictionary<RectTransform, FrameNode> msFrameSized = new Dictionary<RectTransform, FrameNode>();

    private struct Placed
    {
        public Vector2 Set;
        public Vector2 Shift;
        public float Width;
        public float BaseWidth;
    }

    private static readonly Dictionary<RectTransform, Placed> msPlaced = new Dictionary<RectTransform, Placed>();

    private static bool mbHooked;

    /// <summary>The frame whose parts are moved.</summary>
    public static void Attach(RectTransform pOFrame)
    {
        msFrame = pOFrame;

        if (!mbHooked)
        {
            Canvas.willRenderCanvases += fApply;
            mbHooked = true;
        }
    }

    /// <summary>A part in the view's hole: it moves with the hole's middle.</summary>
    public static void InHole(RectTransform pORect)
    {
        if (pORect != null)
            msInHole.Add(pORect);
    }

    /// <summary>A part as wide as the frame's middle: it grows by Extra.</summary>
    public static void Widened(RectTransform pORect)
    {
        if (pORect != null)
            msWidened.Add(pORect);
    }

    /// <summary>A node anchored to the frame's corners (its offsets as they are now, the original's)
    /// whose place lies in the original's frame: it keeps its size, moved as the original place
    /// piX/piRow.</summary>
    public static void FrameSized(RectTransform pORect, int piX, int piRow)
    {
        if (pORect != null)
            msFrameSized[pORect] = new FrameNode { Place = new Vector2Int(piX, piRow), BaseMin = pORect.offsetMin.x, BaseMax = pORect.offsetMax.x };
    }

    /// <summary>Puts every part moved so far back at its original place and forgets it - before a
    /// part goes under another node (the panel's pivot, UWHudPanel), which then moves it, and
    /// before its place is read as the original's.</summary>
    public static void ReleaseAll()
    {
        foreach (KeyValuePair<RectTransform, Placed> lOEntry in msPlaced)
        {
            RectTransform lORect = lOEntry.Key;

            if (lORect == null)
                continue;

            if (lORect.anchoredPosition == lOEntry.Value.Set)
                lORect.anchoredPosition = lOEntry.Value.Set - lOEntry.Value.Shift;

            if (Mathf.Approximately(lORect.sizeDelta.x, lOEntry.Value.Width))
                lORect.sizeDelta = new Vector2(lOEntry.Value.BaseWidth, lORect.sizeDelta.y);
        }

        msPlaced.Clear();

        // The frame-sized nodes too: their own offsets again (a node going under another keeps
        // its rect relative to a frame-filling parent).
        foreach (KeyValuePair<RectTransform, FrameNode> lOEntry in msFrameSized)
        {
            RectTransform lORect = lOEntry.Key;

            if (lORect == null || lORect.parent != msFrame)
                continue;

            lORect.offsetMin = new Vector2(lOEntry.Value.BaseMin, lORect.offsetMin.y);
            lORect.offsetMax = new Vector2(lOEntry.Value.BaseMax, lORect.offsetMax.y);
        }
    }

    /// <summary>
    /// Once a frame from UWGameUI, before anything reads it. WITH THE HELP WINDOW (per user,
    /// 2026-10-10: "the UI only needs to close up as far as needed") the frame keeps as many
    /// columns as leave the help its full width beside it (UWHelpLayout.MaxWidthPerHeight) - set
    /// at once when the help opens and kept until it has closed, so the frame is built once and
    /// only slides; on a window too narrow for that the original's 320 as before.
    /// </summary>
    public static void Update(bool pbClassicScheme, bool pbSuspended)
    {
        if (!pbClassicScheme || pbSuspended || !UWUserSettings.ClassicWidescreen || Screen.height <= 0)
        {
            Extra = 0;
            return;
        }

        float lfCanvasWidth = Screen.width / (Screen.height / UWUiFit.FrameHeight);
        int liFull = fColumnsFor(lfCanvasWidth);

        if (UWHelpLayout.IsOpen || UWHelpLayout.Blend > 0f)
            liFull = Mathf.Min(liFull, fColumnsFor(lfCanvasWidth - (UWHelpLayout.MaxWidthPerHeight * UWUiFit.FrameHeight)));

        Extra = liFull;
    }

    /// <summary>The columns beyond 320 a frame of this width (canvas units) has at 4:3.</summary>
    private static int fColumnsFor(float pfWidth)
    {
        return Mathf.Max(0, Mathf.FloorToInt(pfWidth / UWGameUI.VgaPixelAspectCorrection) - (int)UWUiFit.FrameWidthUnits);
    }

    /// <summary>
    /// An original column from a column of the widened frame at a row from the top: -1 in the
    /// columns put in (they belong to no area of the original's click table).
    /// </summary>
    public static int OriginalX(int piX, int piRow)
    {
        if (Extra <= 0)
            return piX;

        int liCutLeft = UWHudArt.ClassicWideCut(piRow, 0);
        int liCutRight = UWHudArt.ClassicWideCut(piRow, 1);

        if (piX < liCutLeft)
            return piX;

        if (piX < liCutLeft + Left)
            return -1;

        if (piX < liCutRight + Left)
            return piX - Left;

        if (piX < liCutRight + Extra)
            return -1;

        return piX - Extra;
    }

    /// <summary>Moves the frame's parts by their bands, the hole's by its middle, and widens the
    /// message box.</summary>
    private static void fApply()
    {
        if (msFrame == null)
            return;

        int liExtra = Extra;

        for (int liAt = 0; liAt < msFrame.childCount; liAt++)
        {
            RectTransform lORect = msFrame.GetChild(liAt) as RectTransform;

            if (lORect == null)
                continue;

            // Stretched over the frame: the frame picture and the background follow its size, a
            // frame-sized node keeps the original's width at its moved place.
            if (lORect.anchorMin != lORect.anchorMax)
            {
                if (msFrameSized.TryGetValue(lORect, out FrameNode lONode))
                {
                    float lfMove = liExtra > 0 ? UWHudArt.ClassicWideShiftX(lONode.Place.x, lONode.Place.y, liExtra) : 0f;
                    Vector2 lOMin = new Vector2(lONode.BaseMin + lfMove, lORect.offsetMin.y);
                    Vector2 lOMax = new Vector2(lONode.BaseMax + lfMove - liExtra, lORect.offsetMax.y);

                    if (lORect.offsetMin != lOMin)
                        lORect.offsetMin = lOMin;

                    if (lORect.offsetMax != lOMax)
                        lORect.offsetMax = lOMax;
                }

                continue;
            }

            Vector2 lOAt = lORect.anchoredPosition;
            Vector2 lOBase = lOAt;
            float lfBaseWidth = lORect.sizeDelta.x;

            if (msPlaced.TryGetValue(lORect, out Placed lOPlaced))
            {
                // Unchanged since it was moved: its original place is that minus the move.
                if (lOAt == lOPlaced.Set)
                    lOBase = lOAt - lOPlaced.Shift;

                if (Mathf.Approximately(lORect.sizeDelta.x, lOPlaced.Width))
                    lfBaseWidth = lOPlaced.BaseWidth;
            }

            float lfShift = liExtra <= 0 ? 0f
                : msInHole.Contains(lORect) ? liExtra / 2
                : UWHudArt.ClassicWideShiftX(Mathf.FloorToInt(lOBase.x), Mathf.FloorToInt(-lOBase.y), liExtra);
            Vector2 lOShift = new Vector2(lfShift, 0f);
            Vector2 lONew = lOBase + lOShift;
            float lfWidth = msWidened.Contains(lORect) ? lfBaseWidth + liExtra : lfBaseWidth;

            if (lORect.anchoredPosition != lONew)
                lORect.anchoredPosition = lONew;

            if (!Mathf.Approximately(lORect.sizeDelta.x, lfWidth))
                lORect.sizeDelta = new Vector2(lfWidth, lORect.sizeDelta.y);

            msPlaced[lORect] = new Placed { Set = lONew, Shift = lOShift, Width = lfWidth, BaseWidth = lfBaseWidth };
        }
    }
}
