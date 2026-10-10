using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// EVERY TEXT ON THE MODERN FONT'S LINE, WHATEVER ITS FONT (per user, 2026-10-09, after the signs
/// in discs: "a kind of label that takes care of the placing, for every text"). Each font lays its
/// glyphs into the line its own way, so the same text in another font sat a little higher or lower
/// in its rect - and the parts' places are tuned to the modern font. UWUiFonts gives this effect to
/// every text of the modern interface; while a text shows another font than its modern one
/// (Reference), it moves the glyphs up or down so that THE CAPITALS stand where the modern
/// font's would in the same rect: the same middle for a middle anchor, the same top for an upper,
/// the same baseline for a lower one. Measured once per font, size, anchor and rect height on an
/// "H" - never on the text itself, which would make a line jump with its letters ("bag" against
/// "Pack"). Whole pixels, a tie going down. A lone sign in a disc is centred by its ink instead
/// (UWCentredGlyph); the horizontal place stays the generator's.
/// </summary>
[RequireComponent(typeof(Text))]
public class UWFontBaseline : BaseMeshEffect
{
    /// <summary>The font the part chose for the text - the line every other font is put on.</summary>
    public Font Reference;

    private Text mOText;

    private static readonly Dictionary<(Font, Font, int, int, int, float), float> msShifts = new Dictionary<(Font, Font, int, int, int, float), float>();

    private static readonly TextGenerator msGenerator = new TextGenerator();

    public override void ModifyMesh(VertexHelper pOHelper)
    {
        if (!IsActive() || pOHelper.currentVertCount == 0 || Reference == null)
            return;

        if (mOText == null)
            mOText = GetComponent<Text>();

        if (mOText == null || mOText.font == null || mOText.font == Reference)
            return;

        float lfShift = fShift(mOText);

        if (lfShift == 0f)
            return;

        UIVertex lOVertex = new UIVertex();

        for (int liAt = 0; liAt < pOHelper.currentVertCount; liAt++)
        {
            pOHelper.PopulateUIVertex(ref lOVertex, liAt);
            lOVertex.position.y += lfShift;
            pOHelper.SetUIVertex(lOVertex, liAt);
        }
    }

    private float fShift(Text pOText)
    {
        Rect lORect = pOText.rectTransform.rect;
        (Font, Font, int, int, int, float) lsKey = (pOText.font, Reference, pOText.fontSize, (int)pOText.alignment, Mathf.RoundToInt(lORect.height),
            pOText.lineSpacing);

        if (msShifts.TryGetValue(lsKey, out float lfCached))
            return lfCached;

        TextGenerationSettings lOSettings = pOText.GetGenerationSettings(lORect.size);

        lOSettings.horizontalOverflow = HorizontalWrapMode.Overflow;
        lOSettings.verticalOverflow = VerticalWrapMode.Overflow;
        lOSettings.richText = false;

        if (!fCapitals(lOSettings, pOText.font, out float lfBottom, out float lfTop)
            || !fCapitals(lOSettings, Reference, out float lfReferenceBottom, out float lfReferenceTop))
        {
            msShifts[lsKey] = 0f;
            return 0f;
        }

        float lfShift;

        switch (pOText.alignment)
        {
            case TextAnchor.UpperLeft:
            case TextAnchor.UpperCenter:
            case TextAnchor.UpperRight:
                lfShift = lfReferenceTop - lfTop;
                break;

            case TextAnchor.LowerLeft:
            case TextAnchor.LowerCenter:
            case TextAnchor.LowerRight:
                lfShift = lfReferenceBottom - lfBottom;
                break;

            default:
                lfShift = ((lfReferenceBottom + lfReferenceTop) - (lfBottom + lfTop)) * 0.5f;
                break;
        }

        lfShift = Mathf.Floor(lfShift + 0.49f);
        msShifts[lsKey] = lfShift;

        return lfShift;
    }

    /// <summary>Where an "H" in this font stands in the rect: its bottom (the baseline) and top.</summary>
    private static bool fCapitals(TextGenerationSettings pOSettings, Font pOFont, out float pfBottom, out float pfTop)
    {
        pfBottom = 0f;
        pfTop = 0f;
        pOSettings.font = pOFont;

        if (!msGenerator.PopulateWithErrors("H", pOSettings, null) || msGenerator.vertexCount < 4)
            return false;

        pfBottom = float.MaxValue;
        pfTop = float.MinValue;

        foreach (UIVertex lOVertex in msGenerator.verts)
        {
            pfBottom = Mathf.Min(pfBottom, lOVertex.position.y);
            pfTop = Mathf.Max(pfTop, lOVertex.position.y);
        }

        // A TrueType font's glyph quads carry a pixel of padding all round; the original font's are
        // its lit pixels exactly (UWUiFonts).
        if (pOFont.dynamic)
        {
            pfBottom += 1f;
            pfTop -= 1f;
        }

        return pfTop > pfBottom;
    }
}
