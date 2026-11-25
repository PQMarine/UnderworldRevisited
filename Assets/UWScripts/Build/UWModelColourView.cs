using System;
using System.Collections.Generic;
using UnityEngine;
using UWDataImport;
using UWDataImport.UWData;

/// <summary>
/// A 3D MODEL WHOSE COLOURS FOLLOW THE GAME: the large blackrock gem (UWBlackrockGemView, per user 2026-11-16) and the
/// moongate (UWMoongateLook, per user 2026-11-22). Each vertex keeps a key of its face (the gem a colour register, the gate
/// its record colour), and this repaints the vertices whenever the colour a key gets changes. Both paths of the model's
/// solid faces are written: the palette renderer's second UV set (the raw index, its darkness kept) and the vertex colour
/// of the plain one.
/// </summary>
public sealed class UWModelColourView : MonoBehaviour
{
    private Mesh mOMesh;

    private int[] maKeys;

    private List<Vector2> mOPaletteData;

    private List<Color32> mOColors;

    private DataImport mOData;

    private Func<int, int> mOColourOf;

    /// <summary>The colours last painted, by key; a repaint only when one of them changes.</summary>
    private readonly Dictionary<int, int> mOPainted = new Dictionary<int, int>();

    /// <summary>The game clock in the original's 1/256 s ticks (GAME_TIME): the scene's time, which stops with a paused game.</summary>
    public static long GameTime => (long)(Time.time * 256f);

    /// <summary>pOKeys one per vertex (-1: not repainted), pOColourOf a key's colour now (-1: keep the built one).</summary>
    public void Init(Mesh pOMesh, List<int> pOKeys, List<Vector2> pOPaletteData, DataImport pOData, Func<int, int> pOColourOf)
    {
        mOMesh = pOMesh;
        maKeys = pOKeys.ToArray();
        mOPaletteData = new List<Vector2>(pOPaletteData);
        mOColors = new List<Color32>();
        mOMesh.GetColors(mOColors);
        mOData = pOData;
        mOColourOf = pOColourOf;
    }

    private void Update()
    {
        if (mOMesh == null || maKeys == null || mOColourOf == null)
            return;

        bool lbChanged = false;

        foreach (int liKey in maKeys)
        {
            if (liKey < 0)
                continue;

            int liColour = mOColourOf(liKey);

            if (!mOPainted.TryGetValue(liKey, out int liWas) || liWas != liColour)
            {
                mOPainted[liKey] = liColour;
                lbChanged = true;
            }
        }

        if (lbChanged)
            fRepaint();
    }

    private void fRepaint()
    {
        UWPalette lOPalette = mOData != null ? mOData.Palettes.GetPalette(0) : null;

        for (int liAt = 0; liAt < maKeys.Length && liAt < mOPaletteData.Count; liAt++)
        {
            int liKey = maKeys[liAt];

            if (liKey < 0 || !mOPainted.TryGetValue(liKey, out int liColour) || liColour < 0)
                continue;

            // The darkness the build gave the vertex stays (the y of the palette data is it plus one).
            int liDarkness = Mathf.Max(0, (int)mOPaletteData[liAt].y - 1);

            mOPaletteData[liAt] = new Vector2(liColour, liDarkness + 1);

            if (lOPalette != null && liAt < mOColors.Count)
            {
                int liShaded = liDarkness > 0 && mOData.LightLevels != null ? mOData.LightLevels.Remap(liDarkness, liColour) : liColour;
                UWColor32 lOColour = lOPalette.GetUWColor(liShaded);

                mOColors[liAt] = new Color32(lOColour.R, lOColour.G, lOColour.B, 255);
            }
        }

        mOMesh.SetUVs(1, mOPaletteData);

        if (mOColors.Count == maKeys.Length)
            mOMesh.SetColors(mOColors);
    }
}
