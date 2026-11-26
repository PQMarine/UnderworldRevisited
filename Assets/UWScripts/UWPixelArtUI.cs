using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The shared material of the modern UI's pictures in colour (shader UW/PixelArtUI): the
/// leather, the slot circles, the shelf, the flasks and the gem, the paperdoll, the map. At the
/// free scale (UWModernUi.PixelScale) each texel stays a hard square and only the seams between
/// texels are blended. Icons with palette indices take UWIconPalette.ApplySmooth instead.
/// </summary>
public static class UWPixelArtUI
{
    private const string ShaderName = "UW/PixelArtUI";

    private static Material mOMaterial;

    /// <summary>The same shader for the original's own pictures (ApplyOriginal): _UWFromPalette set.</summary>
    private static Material mOOriginalMaterial;

    private static readonly int FromPalette = Shader.PropertyToID("_UWFromPalette");

    private static bool mbMissing;

    /// <summary>
    /// For a picture of the original's own art in palette 0 (the flasks, the power gem, the map): as Apply, and while the
    /// hallucination shows the other palette (UWUiPaletteSwap) its colours follow it as the classic screen's do. The
    /// port's own art (leather, wood, the frames) takes Apply and keeps its colours.
    /// </summary>
    public static void ApplyOriginal(Graphic pOGraphic)
    {
        Apply(pOGraphic);

        if (pOGraphic == null || mOMaterial == null)
            return;

        if (mOOriginalMaterial == null)
        {
            mOOriginalMaterial = new Material(mOMaterial);
            mOOriginalMaterial.name = "UW Pixel art (UI, the original's pictures)";
            mOOriginalMaterial.SetFloat(FromPalette, 1f);
        }

        if (pOGraphic.material != mOOriginalMaterial)
            pOGraphic.material = mOOriginalMaterial;
    }

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

/// <summary>
/// The shared material of the weapon in the hand (shader UW/WeaponUI; per user 2026-11-25, the textures' look, UWTextureLook):
/// the picture blended by the global _UWSpriteFilter as the sprites in the world are, its outline hard; with the filter off
/// (the original's pixels, the palette modes) the texel as the texture's own point filter reads it, the default UI's picture.
/// </summary>
public static class UWWeaponFilterUI
{
    private const string ShaderName = "UW/WeaponUI";

    private static Material mOMaterial;

    private static bool mbMissing;

    /// <summary>Attaches the material; without the shader the default stays (the original's pixels).</summary>
    public static void Apply(Graphic pOGraphic)
    {
        if (pOGraphic == null)
            return;

        if (mOMaterial == null && !mbMissing)
        {
            Shader lOShader = Shader.Find(ShaderName);

            if (lOShader == null)
            {
                Debug.LogError("UI: shader " + ShaderName + " not found - the weapon stays in the original's pixels.");
                mbMissing = true;
                return;
            }

            mOMaterial = new Material(lOShader);
            mOMaterial.name = "UW Weapon (UI)";
        }

        if (mOMaterial != null && pOGraphic.material != mOMaterial)
            pOGraphic.material = mOMaterial;
    }
}
