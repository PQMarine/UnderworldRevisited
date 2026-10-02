// Soft ground shadow under creatures and items, only in the Remastered render mode
// (per user, 2026-09-13: "Carry on with the ground shadows").
//
// A flat quad on the floor that darkens the floor below by MULTIPLYING - this way the
// lighting is preserved: in the dark nothing is visible, in torchlight a soft dark
// spot. No texture, the disc is computed from the texture coordinates.
//
// ORDER: after the dungeon (Geometry) and BEFORE the sprites (AlphaTest). The sprites
// overwrite their pixels, so a spot that lies in front of the feet on screen does
// not darken the feet themselves.
Shader "UW/GroundShadow"
{
    Properties
    {
        _Strength ("Strength", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry+400"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "GroundShadow"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off
            ZWrite Off
            // Pulled slightly towards the camera so the spot does not fight with the floor over
            // depth.
            Offset -1, -1
            Blend DstColor Zero

            HLSLPROGRAM
            #pragma vertex UWVertex
            #pragma fragment UWFragment
            #pragma target 3.0
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Strength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float fogFactor : TEXCOORD1;
            };

            Varyings UWVertex(Attributes IN)
            {
                Varyings OUT = (Varyings)0;

                VertexPositionInputs lOPositions = GetVertexPositionInputs(IN.positionOS.xyz);

                OUT.positionCS = lOPositions.positionCS;
                OUT.uv = IN.uv;
                OUT.fogFactor = ComputeFogFactor(lOPositions.positionCS.z);

                return OUT;
            }

            half4 UWFragment(Varyings IN) : SV_Target
            {
                float lfDistance = length((IN.uv * 2.0) - 1.0);

                // Dark core, soft falloff up to the edge of the quad.
                half lfShape = 1.0h - smoothstep(0.15, 1.0, lfDistance);
                half lfShade = 1.0h - (lfShape * _Strength);

                // In fog the spot disappears together with the floor: mixed towards white means
                // "no effect" when multiplying. MixFogColor knows all fog modes.
                half3 lOShade = MixFogColor(half3(lfShade, lfShade, lfShade), half3(1, 1, 1), IN.fogFactor);

                return half4(lOShade, 1.0h);
            }
            ENDHLSL
        }
    }
}
