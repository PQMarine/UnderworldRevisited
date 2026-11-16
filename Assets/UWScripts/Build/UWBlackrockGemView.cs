using System.Collections.Generic;
using UnityEngine;
using UWDataImport;
using UWDataImport.UWData;

/// <summary>
/// THE LARGE BLACKROCK GEM'S COLOURS IN THE SCENE (UWBlackrockGemLook, per user 2026-11-16): the model's vertices keep the
/// colour register of their face (UWObjectSpawner), and this repaints them whenever the colours the game gives those
/// registers change - the used facets glowing, the facet the gem shows, the body pulsing on the game clock, and the
/// blinking once every gem is used. Both paths of the model's solid faces are written: the palette renderer's second UV
/// set (the raw index, its darkness kept) and the vertex colour of the plain one.
/// </summary>
public sealed class UWBlackrockGemView : MonoBehaviour
{
    private Mesh mOMesh;

    private int[] maRegisters;

    private List<Vector2> mOPaletteData;

    private List<Color32> mOColors;

    private DataImport mOData;

    /// <summary>The colours last painted, by register; a repaint only when one of them changes.</summary>
    private readonly Dictionary<int, int> mOPainted = new Dictionary<int, int>();

    /// <summary>The game clock in the original's 1/256 s ticks (GAME_TIME): the scene's time, which stops with a paused game.</summary>
    private static long fGameTime => (long)(Time.time * 256f);

    /// <summary>The colour the game gives a register now, -1 when the gem does not set it: the quest byte of the used gems
    /// and the facet shown from the second game's player record.</summary>
    public static int ColourNow(int piRegister)
    {
        UWPlayerFile lOFile = UWGameSession.Current != null ? UWGameSession.Current.PlayerFileUw2 : null;
        int liUsed = lOFile != null
            ? UWPlayerRecordUw2.GetNumbered(lOFile, UWTrapRulesUw2.FirstQuestByte + UWTrapRulesUw2.GemsUsedQuestByte) : 0;
        int liShown = lOFile != null ? UWPlayerRecordUw2.GetNumbered(lOFile, UWTrapRulesUw2.GemFacetVariable) : 0;

        return UWBlackrockGemLook.Colour(piRegister, liUsed, liShown, fGameTime);
    }

    public void Init(Mesh pOMesh, List<int> pORegisters, List<Vector2> pOPaletteData, DataImport pOData)
    {
        mOMesh = pOMesh;
        maRegisters = pORegisters.ToArray();
        mOPaletteData = new List<Vector2>(pOPaletteData);
        mOColors = new List<Color32>();
        mOMesh.GetColors(mOColors);
        mOData = pOData;
    }

    private void Update()
    {
        if (mOMesh == null || maRegisters == null)
            return;

        bool lbChanged = false;

        foreach (int liRegister in maRegisters)
        {
            if (liRegister < 0)
                continue;

            int liColour = ColourNow(liRegister);

            if (!mOPainted.TryGetValue(liRegister, out int liWas) || liWas != liColour)
            {
                mOPainted[liRegister] = liColour;
                lbChanged = true;
            }
        }

        if (lbChanged)
            fRepaint();
    }

    private void fRepaint()
    {
        UWPalette lOPalette = mOData != null ? mOData.Palettes.GetPalette(0) : null;

        for (int liAt = 0; liAt < maRegisters.Length && liAt < mOPaletteData.Count; liAt++)
        {
            int liRegister = maRegisters[liAt];

            if (liRegister < 0 || !mOPainted.TryGetValue(liRegister, out int liColour) || liColour < 0)
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

        if (mOColors.Count == maRegisters.Length)
            mOMesh.SetColors(mOColors);
    }
}
