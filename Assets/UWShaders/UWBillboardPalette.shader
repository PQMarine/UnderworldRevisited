// Counterpart to UW/Billboard for the palette renderer.
//
// Same orientation towards the camera, same alpha threshold, same depth offset - only the
// colour is produced differently: the atlas holds no image, but the palette index in the red
// channel and the opacity in the alpha channel (see UWObjectAtlasBuilder and UWCritterAtlasBuilder).
// The lookup chain is in UWPaletteLookup.hlsl.
//
// Nothing of URP remains: no point light, no normal vector, no shadow casting, no
// fog. Brightness depends solely on the player's light level and on distance.
//
// DISTANCE IN THE HORIZONTAL PLANE, from the camera to the sprite's PIVOT, not to the individual
// pixel. In the original a sprite is an image at a tile position and is shaded as a
// whole; measuring per pixel would make the brightness run across the width of the
// graphic, and the edge of a nearby figure would be brighter than its centre.
Shader "UW/BillboardPalette"
{
    Properties
    {
        _IndexTex ("Object atlas (palette indices)", 2D) = "white" {}
        _Cutoff ("Alpha threshold", Range(0, 1)) = 0.5
        _DepthBias ("Offset towards camera (world units)", Float) = 0

        // Set for creatures (UWCritterAnimator) and items (UWLevelLoader), see UWOwnTile.hlsl.
        [Toggle(_UW_OWN_TILE)] _OwnTile ("Own tile does not cover", Float) = 0
        [HideInInspector] _BigRadius ("A big object's radius in eighths (creatures), 0 for items", Float) = 0
        [HideInInspector] _KeyBonus ("Added to the painter sort key (-1 for animations)", Float) = 0
        [HideInInspector] _ChainIndex ("Place in the tile's chain, for ties with 3D models (63 = last)", Float) = 63

        [HideInInspector] _SrcBlend ("Source blend", Float) = 1
        [HideInInspector] _DstBlend ("Destination blend", Float) = 0
        [HideInInspector] _ZWrite ("Write depth", Float) = 1

        // A spell missile glows by itself (UWSpellProjectile, per renderer, per user 2026-10-04).
        [HideInInspector] _Glow ("Glows by itself", Float) = 0
        [HideInInspector] _UWGlowSprite ("A light source's picture (palette glow)", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "TransparentCutout"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "AlphaTest"
        }
        LOD 100

        Pass
        {
            Name "UWUnlit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]

            HLSLPROGRAM
            #pragma vertex UWVertex
            #pragma fragment UWFragment
            #pragma target 3.0
            #pragma multi_compile_local _ _UW_OWN_TILE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            #include "UWPainterOrder.hlsl"
            #include "UWPaletteLookup.hlsl"
            #include "UWPaletteEffects.hlsl"

            TEXTURE2D(_IndexTex);
            SAMPLER(sampler_IndexTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _IndexTex_ST;
                float _Cutoff;
                float _DepthBias;
                float _OwnTile;
                float _BigRadius;
                float _KeyBonus;
                float _ChainIndex;
                float _SrcBlend;
                float _DstBlend;
                float _ZWrite;
                float _Glow;
                float _UWGlowSprite;
            CBUFFER_END

            // NO access to a vertex channel. Sprites only have position and UV; the
            // single-coloured faces of the 3D models go through UW/ModelPalette, precisely
            // so that this shader places no requirement on other meshes. An attempt
            // to do both here broke the animated sprites - their mesh changes
            // per frame, so a channel added afterwards cannot reach all of them
            // (2026-09-05).
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 pivotWS    : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
            };

            Varyings UWVertex(Attributes IN)
            {
                Varyings OUT = (Varyings)0;

                float3 pivotWS = TransformObjectToWorld(float3(0, 0, 0));

                // PARALLEL TO THE SCREEN, as the original draws a sprite - a flat picture scaled by its
                // distance (per user, 2026-09-28: sprites still stabbed through one another). Turned to the
                // camera POSITION, two sprites close together stood at an angle and could cross.
                float3 toCamera = UNITY_MATRIX_V[2].xyz;
                toCamera.y = 0;

                float3 forward = normalize(toCamera + float3(0, 0, 1e-4));
                float3 right = normalize(cross(forward, float3(0, 1, 0)));

                float3 positionWS = pivotWS + right * IN.positionOS.x + float3(0, 1, 0) * IN.positionOS.y + forward * _DepthBias;

                OUT.positionCS = TransformWorldToHClip(positionWS);
                OUT.pivotWS = pivotWS;
                OUT.positionWS = positionWS;
                OUT.uv = TRANSFORM_TEX(IN.uv, _IndexTex);

                return OUT;
            }

            // THE ORIGINAL'S DRAW ORDER (UWPainterOrder.hlsl), for every sprite in the palette path:
            // against the geometry by the tile order of the surface behind the pixel, among the
            // sprites by one depth for the whole sprite in a band in front of the geometry, so none
            // stabs through another.
            half4 UWFragment(Varyings IN, out float outDepth : SV_Depth) : SV_Target
            {
                half4 raw = SAMPLE_TEXTURE2D(_IndexTex, sampler_IndexTex, IN.uv);

                clip(raw.a - _Cutoff);

                float2 lTile;
                float lfPart;

                UWPainterObjectPlace(IN.pivotWS, _BigRadius, _KeyBonus, lTile, lfPart);

                // Not drawn by the original: neither its own tile nor the one a big creature is
                // handed to is marked by the sweep (UWPainterSweepHides).
                if (UWPainterSweepHides(UWPainterTileOf(IN.pivotWS.xz)) && UWPainterSweepHides(lTile))
                    discard;

                // Outside the original's own cone the real depth decides (UWPainterSweepTrueDepth).
                bool lbTrueDepth = UWPainterSweepTrueDepth(UWPainterTileOf(IN.pivotWS.xz));

                if (lbTrueDepth ? !UWPainterSpriteVisibleTrue(IN.positionCS.xy, lTile, IN.pivotWS, IN.positionWS)
                    : !UWPainterSpriteVisible(IN.positionCS.xy, lTile, IN.pivotWS, lfPart, _ChainIndex))
                    discard;

                outDepth = UWPainterSpriteBandDepth(UWPainterDepth(lTile, lfPart, UWPainterNearness(IN.pivotWS)));

                // A translucent pixel replaces what lies behind it through its XFER table -
                // read from the copy of the picture made after everything solid was drawn
                // (such sprites are drawn after it, in the transparent queue). Full opacity:
                // the mixing is in the table, not in the blend.
                float lfTable = UWXferTable(raw);

                // Under the hallucination's light table the marker itself goes through the table
                // first: LIGHT.DAT keeps 248 to 255 at every level, so the original evidently looks
                // the index up and only then sees the marker - the effect's table sends it to black
                // (or to one of its dots), and the ghost vanishes (per user, 2026-09-27).
                if (lfTable >= 0.0 && _UWLightEffect > 0.5)
                {
                    return UWShadeIndex(UW_XFER_FIRST_MARKER + lfTable,
                        UWTileDistance(IN.pivotWS, _WorldSpaceCameraPos), 0.0, IN.positionCS.xy);
                }

                if (lfTable >= 0.0)
                {
                    half3 lOBehind = LOAD_TEXTURE2D_X(_CameraOpaqueTexture, uint2(IN.positionCS.xy)).rgb;

                    return half4(UWXferColour(lfTable, lOBehind), 1.0h);
                }

                // A SPELL MISSILE GLOWS: the palette's own colour, as at full brightness - neither
                // the distance nor the light level darkens it (per user, 2026-10-04).
                if (_Glow > 0.5)
                    return half4(UWShadeLookup(UWRotateIndex(UWToIndex(raw.r)), 0.0).rgb, raw.a);

                // A LIGHT SOURCE'S BRIGHT PIXELS GLOW (UWPaletteEffects.hlsl, the palette glow):
                // their full colour, the rest of the picture shaded as usual.
                if (_UWGlowSprite > 0.5 && UWGlowInReach(IN.pivotWS))
                {
                    half3 lOFull = UWShadeLookup(UWRotateIndex(UWToIndex(raw.r)), 0.0).rgb;

                    if (max(lOFull.r, max(lOFull.g, lOFull.b)) > UW_GLOW_THRESHOLD)
                        return half4(lOFull, raw.a);
                }

                // The light sources at the sprite's ground point (UWPaletteEffects.hlsl).
                UWLightCap = UWPaletteLightCap(IN.pivotWS, float3(0.0, 0.0, 0.0));

                return UWPaletteColour(raw, IN.pivotWS, _WorldSpaceCameraPos, IN.positionCS.xy);
            }
            ENDHLSL
        }

        // NO DEPTH PASS: the sprites must stay out of the depth prepass, whose texture shows them
        // the geometry behind them (UWPainterSpriteVisible).
    }

    FallBack Off
}
