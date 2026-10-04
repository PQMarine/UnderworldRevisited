using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The shared material of the modern UI's pictures in colour (shader UW/PixelArtUI): the
/// leather, the slot circles, the shelf, the flasks and the gem, the paperdoll, the map. At the
/// free scale (UWModernHud.PixelScale) each texel stays a hard square and only the seams between
/// texels are blended. Icons with palette indices take UWIconPalette.ApplySmooth instead.
/// </summary>
public static class UWPixelArtUI
{
    private const string ShaderName = "UW/PixelArtUI";

    private static Material mOMaterial;

    private static bool mbMissing;

    /// <summary>Attaches the material; without the shader the default stays (point filtered).</summary>
    public static void Apply(Graphic pOGraphic)
    {
        if (pOGraphic == null)
            return;

        if (mOMaterial == null && !mbMissing)
        {
            Shader lOShader = Shader.Find(ShaderName);

            if (lOShader == null)
            {
                Debug.LogError("UI: shader " + ShaderName + " not found - the modern UI's pictures stay point filtered.");
                mbMissing = true;
                return;
            }

            mOMaterial = new Material(lOShader);
            mOMaterial.name = "UW Pixel art (UI)";
        }

        if (mOMaterial != null && pOGraphic.material != mOMaterial)
            pOGraphic.material = mOMaterial;
    }
}
