using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// THE DEBUG TOOLS' LABELS IN THE WORLD ON THE KIT (cleanup 8c, 2026-10-25): in full-bright mode every
/// trap and trigger of the level is named at its position and each trigger has a line to its target
/// tile (UWDebugTools.fDrawInvisibleObjects). IMGUI drew them in OnGUI until then - a label rectangle
/// and a rotated strip; here a pool of uGUI texts and thin images on a canvas of their own
/// (UWUiSorting.DebugLabels) at screen pixels, each set only where something changed, and what a
/// frame leaves unused hides. The look is IMGUI's: gold text centred on its point, the line half
/// see-through; the text a little larger with the window and with a shadow, so it reads on bright walls.
/// </summary>
internal sealed class UWDebugLabels
{
    private static readonly Color LabelColour = new Color(1f, 0.85f, 0.3f);

    private static readonly Color LineColour = new Color(1f, 0.85f, 0.3f, 0.5f);

    /// <summary>The IMGUI label's size and its rectangle round the point (180 x 32).</summary>
    private const int LabelSize = 11;

    private const float LabelWidth = 180f;

    private const float LabelHeight = 32f;

    private const float LineThickness = 2f;

    private readonly Transform mOOwner;

    private Canvas mOCanvas;

    /// <summary>The lines under the labels, as IMGUI drew each line before its label.</summary>
    private RectTransform mOLineGroup;

    private RectTransform mOLabelGroup;

    private readonly List<Text> mOLabels = new List<Text>();

    private readonly List<Image> mOLines = new List<Image>();

    private int miLabel;

    private int miLine;

    public UWDebugLabels(Transform pOOwner)
    {
        mOOwner = pOOwner;
    }

    /// <summary>Starts a frame of labels; hidden, the canvas goes dark and nothing is drawn.</summary>
    public void Begin(bool pbShown)
    {
        miLabel = 0;
        miLine = 0;

        if (!pbShown)
        {
            if (mOCanvas != null && mOCanvas.enabled)
                mOCanvas.enabled = false;

            return;
        }

        if (mOCanvas == null)
        {
            mOCanvas = UWUiKit.CreateOverlayCanvas(mOOwner, "Debug Labels Canvas", UWUiSorting.DebugLabels);
            mOLineGroup = UWUiKit.CreateStretchedGroup(mOCanvas.transform, "Lines");
            mOLabelGroup = UWUiKit.CreateStretchedGroup(mOCanvas.transform, "Labels");
        }

        if (!mOCanvas.enabled)
            mOCanvas.enabled = true;
    }

    /// <summary>A label centred on a screen point (pixels, y up).</summary>
    public void Label(Vector2 pOAt, string psText)
    {
        Text lOText;

        if (miLabel < mOLabels.Count)
            lOText = mOLabels[miLabel];
        else
        {
            lOText = UWUiKit.CreateText(mOLabelGroup, "Label", UWUiKit.BottomLeft, UWUiKit.Centre, TextAnchor.MiddleCenter, LabelColour, null,
                new UWUiKit.TextStyle { Font = UWUiKit.TextFontEnum.Interface, Effect = UWUiKit.TextEffectEnum.Shadow, KeepsItsFont = true });
            lOText.supportRichText = false;
            mOLabels.Add(lOText);
        }

        miLabel++;

        int liSize = Mathf.Max(LabelSize, Mathf.RoundToInt(LabelSize * UWGuiTheme.Scale));
        Vector2 lOSize = new Vector2(LabelWidth, LabelHeight) * Mathf.Max(1f, UWGuiTheme.Scale);

        if (!lOText.gameObject.activeSelf)
            lOText.gameObject.SetActive(true);

        if (lOText.text != psText)
            lOText.text = psText;

        if (lOText.fontSize != liSize)
            lOText.fontSize = liSize;

        if (lOText.rectTransform.anchoredPosition != pOAt)
            lOText.rectTransform.anchoredPosition = pOAt;

        if (lOText.rectTransform.sizeDelta != lOSize)
            lOText.rectTransform.sizeDelta = lOSize;
    }

    /// <summary>A line between two screen points (pixels, y up).</summary>
    public void Line(Vector2 pOFrom, Vector2 pOTo)
    {
        Vector2 lODelta = pOTo - pOFrom;
        float lfLength = lODelta.magnitude;

        if (lfLength < 1f)
            return;

        Image lOLine;

        if (miLine < mOLines.Count)
            lOLine = mOLines[miLine];
        else
        {
            lOLine = UWUiKit.CreateImage(mOLineGroup, "Line", UWUiKit.BottomLeft, new Vector2(0f, 0.5f), LineColour);
            mOLines.Add(lOLine);
        }

        miLine++;

        if (!lOLine.gameObject.activeSelf)
            lOLine.gameObject.SetActive(true);

        RectTransform lORect = lOLine.rectTransform;

        lORect.anchoredPosition = pOFrom;
        lORect.sizeDelta = new Vector2(lfLength, LineThickness);
        lORect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(lODelta.y, lODelta.x) * Mathf.Rad2Deg);
    }

    /// <summary>Ends the frame: the labels and lines it did not use hide.</summary>
    public void End()
    {
        for (int liAt = miLabel; liAt < mOLabels.Count; liAt++)
        {
            if (mOLabels[liAt].gameObject.activeSelf)
                mOLabels[liAt].gameObject.SetActive(false);
        }

        for (int liAt = miLine; liAt < mOLines.Count; liAt++)
        {
            if (mOLines[liAt].gameObject.activeSelf)
                mOLines[liAt].gameObject.SetActive(false);
        }
    }
}
