using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The hallucination's other palette on the interface too (UWHallucinationState.PaletteEffect):
/// the original swaps the VGA palette, and the whole screen changes - panels, paperdoll, flasks,
/// the message scroll (per user with screenshots of the original, 2026-09-27; ours changed only
/// the 3D view and the icons drawn through UW/IconPalette).
///
/// While the effect runs, every interface Graphic still on the default UI material gets the
/// material of UW/UIPaletteSwap, which paints each of its colours in the hallucination's palette;
/// what appears meanwhile (a new line in the scroll, the icon on the pointer) is picked up by a
/// scan four times a second. When the effect ends, every Graphic gets its material back. The
/// icons on UW/IconPalette follow the colour table by themselves and are left alone.
/// </summary>
public static class UWUiPaletteSwap
{
    private const string ShaderName = "UW/UIPaletteSwap";

    private const string DefaultShaderName = "UI/Default";

    private const float ScanSeconds = 0.25f;

    private static Material msMaterial;

    /// <summary>What each swapped Graphic had set before: null for the default material.</summary>
    private static readonly Dictionary<Graphic, Material> msOriginals = new Dictionary<Graphic, Material>();

    private static float msfNextScan;

    /// <summary>Called every frame by UWPaletteRenderToggle.</summary>
    public static void Follow(bool pbActive)
    {
        if (!pbActive)
        {
            fRestore();

            return;
        }

        if (Time.unscaledTime < msfNextScan)
            return;

        msfNextScan = Time.unscaledTime + ScanSeconds;

        if (msMaterial == null)
        {
            Shader lOShader = Shader.Find(ShaderName);

            if (lOShader == null)
                return;

            msMaterial = new Material(lOShader) { name = "UW Interface (hallucination palette)" };
        }

        foreach (Graphic lOGraphic in Object.FindObjectsByType<Graphic>(FindObjectsInactive.Exclude))
        {
            if (lOGraphic == null || msOriginals.ContainsKey(lOGraphic))
                continue;

            Material lOCurrent = lOGraphic.material;

            if (lOCurrent == null || lOCurrent.shader == null || lOCurrent.shader.name != DefaultShaderName)
                continue;

            msOriginals[lOGraphic] = lOCurrent == lOGraphic.defaultMaterial ? null : lOCurrent;
            lOGraphic.material = msMaterial;
        }
    }

    private static void fRestore()
    {
        if (msOriginals.Count == 0)
            return;

        foreach (KeyValuePair<Graphic, Material> lOPair in msOriginals)
        {
            if (lOPair.Key != null)
                lOPair.Key.material = lOPair.Value;
        }

        msOriginals.Clear();
        msfNextScan = 0f;
    }
}
