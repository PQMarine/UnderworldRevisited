using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The shared material of the UI icons in the palette renderer.
///
/// The icon textures carry palette indices instead of colours (see UWIconTextureBuilder); only
/// the shader UW/IconPalette turns them into colours, via the same lookup textures as the
/// world. Because all icons share the same material, the batching of the
/// UI does not change.
///
/// ONE material for all: it carries no values of its own apart from the shader. The texture comes
/// per component from Unity itself (Image and RawImage set it as _MainTex), the
/// lookup textures come as global shader values from UWShadePalette.ApplyGlobals.
/// </summary>
public static class UWIconPalette
{
    private const string ShaderName = "UW/IconPalette";

    private static Material mOMaterial;

    private static bool mbMissing;

    /// <summary>
    /// Attaches the palette material to a UI component.
    ///
    /// Called after every assignment of an icon texture. If the shader is not found,
    /// the component stays on the default material - then you see garbage instead of nothing,
    /// which is more helpful when searching than an empty field.
    /// </summary>
    public static void Apply(Graphic pOGraphic)
    {
        if (pOGraphic == null)
            return;

        Material lOMaterial = fGetMaterial();

        if (lOMaterial == null)
            return;

        if (pOGraphic.material != lOMaterial)
            pOGraphic.material = lOMaterial;
    }

    private static Material mOSmoothMaterial;

    /// <summary>
    /// The same with the shader's UW_PIXEL_SMOOTH: for the modern UI, whose free scale is often
    /// no whole number (UWModernHud.PixelScale) - each texel stays a hard square, only the seams
    /// are blended. One shared material as well, so batching stays.
    /// </summary>
    public static void ApplySmooth(Graphic pOGraphic)
    {
        if (pOGraphic == null)
            return;

        if (mOSmoothMaterial == null)
        {
            Material lOBase = fGetMaterial();

            if (lOBase == null)
                return;

            mOSmoothMaterial = new Material(lOBase);
            mOSmoothMaterial.name = "UW Icons (Palette, smooth)";
            mOSmoothMaterial.EnableKeyword("UW_PIXEL_SMOOTH");
        }

        if (pOGraphic.material != mOSmoothMaterial)
            pOGraphic.material = mOSmoothMaterial;
    }

    private static Material fGetMaterial()
    {
        if (mOMaterial != null || mbMissing)
            return mOMaterial;

        Shader lOShader = Shader.Find(ShaderName);

        if (lOShader == null)
        {
            Debug.LogError("UI: shader " + ShaderName + " not found - icons stay on the default material.");

            mbMissing = true;

            return null;
        }

        mOMaterial = new Material(lOShader);
        mOMaterial.name = "UW Icons (Palette)";

        return mOMaterial;
    }
}
