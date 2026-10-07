// Counterpart to UW/Decal for the palette renderer: everything that comes from the object atlas
// but is not rotated towards the camera - doors, wall writings, switches, bridges.
//
// The atlas holds no image, but the palette index in the red channel and the opacity in the
// alpha channel (see UWObjectAtlasBuilder). The lookup chain is in UWPaletteLookup.hlsl.
//
// DISTANCE PER PIXEL, not to the pivot as with a sprite: these surfaces are fixed
// in space and should shade across their depth.
//
// THE VERTEX COLOUR IS NOT READ, unlike in the colour path. There it multiplies the
// texture, which does not work with an index - half an index is not half a colour. The
// single-coloured surfaces of the 3D models and the portcullis (whose bar shading comes about
// this way) have therefore moved out and run through UW/ModelPalette, where the darkening
// hangs on the vertex as a separate number (see UWPaletteRenderToggle).
Shader "UW/DecalPalette"
{
    Properties
    {
        _IndexTex ("Object atlas (palette indices)", 2D) = "white" {}
        _Cutoff ("Alpha cutoff", Range(0, 1)) = 0.5

        // Double-sided unless a panel asks otherwise: the wall panels 366/367 are drawn from the
        // front only, as the original's model 0x16 (UWObjectSpawner.fGetSingleSided).
        [HideInInspector] _Cull ("Cull", Float) = 0
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

            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex UWVertex
            #pragma fragment UWFragment
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "UWPaletteLookup.hlsl"
            #include "UWPaletteEffects.hlsl"

            TEXTURE2D(_IndexTex);
            SAMPLER(sampler_IndexTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _IndexTex_ST;
                float _Cutoff;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            Varyings UWVertex(Attributes IN)
            {
                Varyings OUT = (Varyings)0;

                VertexPositionInputs positions = GetVertexPositionInputs(IN.positionOS.xyz);

                OUT.positionCS = positions.positionCS;
                OUT.positionWS = positions.positionWS;
                OUT.uv = TRANSFORM_TEX(IN.uv, _IndexTex);

                return OUT;
            }

            half4 UWFragment(Varyings IN) : SV_Target
            {
                half4 raw = SAMPLE_TEXTURE2D(_IndexTex, sampler_IndexTex, IN.uv);

                clip(raw.a - _Cutoff);

                UWLightCap = UWPaletteLightCap(IN.positionWS, UWFacingNormal(IN.positionWS));

                half4 colour = UWPaletteColourExtra(raw, UWPaletteEffectLevel(IN.positionCS),
                    IN.positionWS, _WorldSpaceCameraPos, IN.positionCS.xy);

                return half4(colour.rgb, 1.0h);
            }
            ENDHLSL
        }

        // In the depth prepass, so a sprite sees a closed door or a bridge in front of it
        // (UWPainterSpriteVisible reads that depth).
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex UWDepthVertex
            #pragma fragment UWDepthFragment
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_IndexTex);
            SAMPLER(sampler_IndexTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _IndexTex_ST;
                float _Cutoff;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            Varyings UWDepthVertex(Attributes IN)
            {
                Varyings OUT = (Varyings)0;

                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _IndexTex);

                return OUT;
            }

            half4 UWDepthFragment(Varyings IN) : SV_Target
            {
                clip(SAMPLE_TEXTURE2D(_IndexTex, sampler_IndexTex, IN.uv).a - _Cutoff);

                return 0;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
