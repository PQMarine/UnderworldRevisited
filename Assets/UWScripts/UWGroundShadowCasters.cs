using UnityEngine;
using UWDataImport.UWData;

/// <summary>
/// The Unity side of the ONE ground-shadow rule (UWGroundShadowRule, cleanup step 6.4,
/// 2026-10-24): whether a world entity casts a blob and how wide it is, asked by the Remastered
/// blobs (UWRemasterGroundShadows) and the palette path's ground shadows (UWPaletteEffects).
/// What the rule needs from Unity is here - in flight (UWProjectileFlight), standing as a sprite
/// (the billboard shaders), the collider's size; the decision and the numbers are the rule's.
/// </summary>
public static class UWGroundShadowCasters
{
    public const string BillboardShaderName = "UW/Billboard";

    /// <summary>The palette path's twin of the billboard shader (UWPaletteRenderPath swaps the
    /// materials).</summary>
    public const string BillboardPaletteShaderName = "UW/BillboardPalette";

    /// <summary>
    /// Whether the entity casts a ground blob, and its width in plan view. The palette path also
    /// takes sprites on the palette shader; the Remastered blobs take UW/Billboard only, exactly
    /// as each path did before the step.
    /// </summary>
    public static bool TryGetWidth(UWEntityInfo pOEntity, bool pbPaletteSprites, out float pfWidth)
    {
        pfWidth = 0f;

        if (pOEntity == null || pOEntity.ObjectData == null)
            return false;

        if (!UWGroundShadowRule.Casts(pOEntity.ObjectData.ID, pOEntity.GetComponent<UWProjectileFlight>() != null))
            return false;

        MeshRenderer lORenderer = pOEntity.GetComponent<MeshRenderer>();
        Shader lOShader = lORenderer != null && lORenderer.sharedMaterial != null ? lORenderer.sharedMaterial.shader : null;

        if (lOShader == null)
            return false;

        string lsShader = lOShader.name;

        if (lsShader != BillboardShaderName && !(pbPaletteSprites && lsShader == BillboardPaletteShaderName))
            return false;

        BoxCollider lOCollider = pOEntity.GetComponent<BoxCollider>();

        pfWidth = lOCollider != null
            ? UWGroundShadowRule.Width(true, lOCollider.size.x, lOCollider.size.z)
            : UWGroundShadowRule.Width(false, 0f, 0f);

        return true;
    }
}
