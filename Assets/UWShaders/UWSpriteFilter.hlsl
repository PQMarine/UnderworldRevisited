// THE TEXTURES' LOOK FOR THE SPRITES (per user 2026-11-25, UWTextureLook): the things, creatures, doors, pictures on
// walls and models' faces drawn from the atlases, blended between their texels by the shader itself, so the atlases stay
// point filtered and nothing is rebuilt when the look changes (UWRemasterRenderPath sets _UWSpriteFilter every frame, 0
// outside Remastered).
//
// THE OUTLINE STAYS HARD (per user: only the inside is smoothed): the opacity is the nearest texel's, so a cut-out picture
// keeps its silhouette and the alpha test cuts where it always did. The colour is the four texels around the point
// mixed by their opacity (premultiplied), so a clear neighbour - the atlas's padding, the picture's outside - adds no
// colour and leaves no dark fringe, and no piece bleeds into the next.
//
// 1 SMOOTH: bilinear between the four texel centres, as the 2012 version's textures (per user 2026-11-25, "Bilinear wie
// 2012"). No mipmaps: the atlases have none.
#ifndef UW_SPRITE_FILTER_INCLUDED
#define UW_SPRITE_FILTER_INCLUDED

float _UWSpriteFilter;

half4 UWSpriteFilterPremultiplied(half4 pTexel)
{
    return half4(pTexel.rgb * pTexel.a, pTexel.a);
}

// pNearest is the texel as the material's own sampler reads it (point filtered): returned as it is with the filter off,
// and its opacity kept with it on. pTexelSize is the texture's _TexelSize (1/width, 1/height, width, height).
half4 UWSpriteFilterColour(TEXTURE2D_PARAM(pTexture, pSampler), float2 pUv, float4 pTexelSize, half4 pNearest)
{
    if (_UWSpriteFilter < 0.5 || pNearest.a <= 0.0h)
        return pNearest;

    float2 lSize = pTexelSize.zw;
    float2 lAt = pUv * lSize - 0.5;
    float2 lBase = floor(lAt);
    float2 lBlend = lAt - lBase;

    float2 lLow = (lBase + 0.5) * pTexelSize.xy;
    float2 lHigh = (lBase + 1.5) * pTexelSize.xy;

    half4 l00 = UWSpriteFilterPremultiplied(SAMPLE_TEXTURE2D_LOD(pTexture, sampler_PointClamp, lLow, 0));
    half4 l10 = UWSpriteFilterPremultiplied(SAMPLE_TEXTURE2D_LOD(pTexture, sampler_PointClamp, float2(lHigh.x, lLow.y), 0));
    half4 l01 = UWSpriteFilterPremultiplied(SAMPLE_TEXTURE2D_LOD(pTexture, sampler_PointClamp, float2(lLow.x, lHigh.y), 0));
    half4 l11 = UWSpriteFilterPremultiplied(SAMPLE_TEXTURE2D_LOD(pTexture, sampler_PointClamp, lHigh, 0));
    half4 lMixed = lerp(lerp(l00, l10, lBlend.x), lerp(l01, l11, lBlend.x), lBlend.y);

    return half4(lMixed.a > 0.0001h ? lMixed.rgb / lMixed.a : pNearest.rgb, pNearest.a);
}

#endif
