// THE PALETTE RENDERER'S SHADER for floor, ceiling and walls. In use when the render mode
// is Palette (see UWLevelAssembler.CreateMaterial and UWPaletteRenderToggle);
// UWDungeon.shader covers the URP path with its Remastered effects.
//
// Counterpart to UW/Dungeon, but without any URP lighting. The texture array holds no
// colours but palette indices (see UWTextureArrayBuilder.BuildIndexed); the colour
// is only produced here via the tables from UWShadePalette. The lookup chain itself
// lives in UWPaletteLookup.hlsl.
//
// WHAT GOES AWAY in this path compared to UW/Dungeon:
//   - all point lights in the dungeon including the range quadrupling and near-range clamping
//   - shadow casting and the normal vector it needs
//   - the base brightness value that currently keeps corners from going fully black
//   - copying the water and lava images every frame (UWAnimatedTextures)
//
// The view distance is already baked into the colour table: beyond the view distance it holds
// shade level 15, i.e. practically black. A separate cutoff is therefore only a
// matter of speed, not of appearance.
Shader "UW/DungeonPalette"
{
    Properties
    {
        _IndexArray ("Level textures (palette indices)", 2DArray) = "" {}
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }
        LOD 100

        Pass
        {
            Name "UWUnlit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex UWVertex
            #pragma fragment UWFragment
            #pragma target 3.5
            #pragma require 2darray

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "UWHallucination.hlsl"

            TEXTURE2D_ARRAY(_IndexArray);
            SAMPLER(sampler_IndexArray);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 uv         : TEXCOORD0;
            };

            // NO access to a vertex channel - see UW/ModelPalette.
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            Varyings UWVertex(Attributes IN)
            {
                Varyings OUT = (Varyings)0;

                VertexPositionInputs positions = GetVertexPositionInputs(IN.positionOS.xyz);

                OUT.positionCS = positions.positionCS;
                OUT.positionWS = positions.positionWS;
                OUT.uv = IN.uv;

                return OUT;
            }

            // The scrambled texture mapper of the hallucination: UWScrambleUV, UWHallucination.hlsl.

            half4 UWFragment(Varyings IN) : SV_Target
            {
                half4 raw = SAMPLE_TEXTURE2D_ARRAY(_IndexArray, sampler_IndexArray, UWScrambleUV(IN.uv), IN.uv.z);

                // The part of the hallucination's light table (UWHallucination.hlsl).
                UWSurfacePart = UWDungeonSurfacePart(IN.uv.z, IN.positionWS.y);

                half4 colour = UWPaletteColour(raw, IN.positionWS, _WorldSpaceCameraPos,
                    IN.positionCS.xy);

                return half4(colour.rgb, 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }
    }

    FallBack Off
}
