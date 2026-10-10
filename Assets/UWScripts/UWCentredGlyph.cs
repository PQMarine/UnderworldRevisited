using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A SIGN CENTRED BY ITS INK (per user, 2026-10-09: "for the elements with a small circle - the
/// minimap's + and -, the bags' numbers - a control that puts the sign exactly in the middle,
/// whatever the font"). A Text centres its line box, and every font lays its glyphs into the line
/// its own way - a descenders' row, a spacing column, padding -, so a lone sign in a circle sat a
/// little off, differently per font. This effect, on the Text, measures the glyphs the text
/// generator drew and moves them so their middle lies on the rect's middle, in whole pixels. The
/// generator's glyphs, not the mesh: the outline and shadow copies come before it and would pull
/// the middle aside. For the original font the glyph quads are its lit pixels only (UWUiFonts),
/// for a TrueType font Unity's quads with the same padding on every side.
/// </summary>
[RequireComponent(typeof(Text))]
public class UWCentredGlyph : BaseMeshEffect
{
    private Text mOText;

    public override void ModifyMesh(VertexHelper pOHelper)
    {
        if (!IsActive() || pOHelper.currentVertCount == 0)
            return;

        if (mOText == null)
            mOText = GetComponent<Text>();

        TextGenerator lOGenerator = mOText != null ? mOText.cachedTextGenerator : null;

        if (lOGenerator == null || lOGenerator.vertexCount == 0)
            return;

        float lfUnit = mOText.pixelsPerUnit > 0f ? 1f / mOText.pixelsPerUnit : 1f;
        Vector2 lOMin = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 lOMax = new Vector2(float.MinValue, float.MinValue);

        foreach (UIVertex lOVertex in lOGenerator.verts)
        {
            Vector2 lOAt = lOVertex.position * lfUnit;

            lOMin = Vector2.Min(lOMin, lOAt);
            lOMax = Vector2.Max(lOMax, lOAt);
        }

        if (lOMin.x > lOMax.x)
            return;

        Vector2 lOShift = mOText.rectTransform.rect.center - ((lOMin + lOMax) * 0.5f);

        // Whole pixels; a tie (odd ink in an even rect) goes left and down - rounded to the right a
        // 5-pixel "3" sat half a pixel right of its ring (per user, 2026-10-09).
        lOShift = new Vector2(Mathf.Floor(lOShift.x + 0.49f), Mathf.Floor(lOShift.y + 0.49f));

        if (lOShift == Vector2.zero)
            return;

        UIVertex lOOut = new UIVertex();

        for (int liAt = 0; liAt < pOHelper.currentVertCount; liAt++)
        {
            pOHelper.PopulateUIVertex(ref lOOut, liAt);
            lOOut.position += (Vector3)lOShift;
            pOHelper.SetUIVertex(lOOut, liAt);
        }
    }
}
