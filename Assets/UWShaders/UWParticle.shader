// Particles for the Remastered render mode: sparks and smoke at open fires, dust in the
// air (per user, 2026-09-13: "fire and dust").
//
// No image: the dot is computed from the texture coordinates of the particle quad as a soft
// disc. So no texture of its own is needed, and no pixel grid
// fights against the pixel art of the world.
//
// Blending is PREMULTIPLIED (One, OneMinusSrcAlpha). That way one material covers both:
// _Additive 1 writes no alpha and only adds light (sparks), _Additive 0 covers like
// ordinary smoke.
//
// _Lit 1 lets the surrounding point lights colour the particle - without a normal, a
// dust mote glows from all sides. So dust is only visible where light falls.
Shader "UW/Particle"
{
    Properties
    {
        _Intensity ("Brightness", Range(0, 8)) = 1
        _Additive ("Additive (sparks) instead of opaque (smoke)", Range(0, 1)) = 1
        _Lit ("Lit by point lights", Range(0, 1)) = 0
        _Softness ("Edge softness", Range(0.05, 1)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off
            ZWrite Off
            Blend One OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex UWVertex
            #pragma fragment UWFragment
            #pragma target 3.5

            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Intensity;
                float _Additive;
                float _Lit;
                float _Softness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                half4 color : COLOR;
                float fogFactor : TEXCOORD2;
            };

            Varyings UWVertex(Attributes IN)
            {
                Varyings OUT = (Varyings)0;

                VertexPositionInputs lOPositions = GetVertexPositionInputs(IN.positionOS.xyz);

                OUT.positionCS = lOPositions.positionCS;
                OUT.positionWS = lOPositions.positionWS;
                OUT.uv = IN.uv;
                OUT.color = IN.color;
                OUT.fogFactor = ComputeFogFactor(lOPositions.positionCS.z);

                return OUT;
            }

            half4 UWFragment(Varyings IN) : SV_Target
            {
                // Soft disc: 1 in the centre, 0 at the edge.
                float lfDistance = length((IN.uv * 2.0) - 1.0);
                half lfShape = saturate((1.0 - lfDistance) / max(_Softness, 0.05));

                lfShape *= lfShape;

                half3 lOColour = IN.color.rgb * _Intensity;

                if (_Lit > 0.5)
                {
                    half3 lOLight = half3(0, 0, 0);

                    #if defined(_ADDITIONAL_LIGHTS)
                    uint lightCount = GetAdditionalLightsCount();

                    InputData inputData = (InputData)0;
                    inputData.positionWS = IN.positionWS;
                    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionCS);

                    LIGHT_LOOP_BEGIN(lightCount)
                        Light light = GetAdditionalLight(lightIndex, IN.positionWS, half4(1, 1, 1, 1));
                        lOLight += light.color * light.distanceAttenuation;
                    LIGHT_LOOP_END
                    #endif

                    // Softly capped: close to the torch, one over distance squared goes towards
                    // infinity; the mote should be bright there, but not burn out to white.
                    half lfPeak = max(lOLight.r, max(lOLight.g, lOLight.b));

                    lOLight *= 1.0 / (1.0 + lfPeak);
                    lOColour *= lOLight;
                }

                half lfAlpha = lfShape * IN.color.a;
                half3 lOPremultiplied = lOColour * lfAlpha;

                lOPremultiplied = MixFogColor(lOPremultiplied, half3(0, 0, 0), IN.fogFactor);

                return half4(lOPremultiplied, lfAlpha * (1.0 - _Additive));
            }
            ENDHLSL
        }
    }
}
