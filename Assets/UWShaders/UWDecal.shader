// Shader for everything that comes from the object atlas but is not rotated towards the camera:
// doors, wall decorations, switches, bridges.
Shader "UW/Decal"
{
    Properties
    {
        _MainTex ("Object Atlas", 2D) = "white" {}
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        _AmbientFloor ("Ambient Floor", Range(0, 1)) = 0.08
        _PointLightCap ("Point Light Cap (Close Range)", Range(0.3, 5)) = 1.4

        // The index version of the atlas and, for the solid-colour model faces and the portcullis
        // (1), the palette index on the vertex instead (UV set 1: x the index, y the darkening plus
        // one, as UW/ModelPalette reads it). Read for the hallucination's pictures only
        // (UWHallucination.hlsl); black, i.e. nothing, where a material has no index atlas.
        _IndexTex ("Index Atlas", 2D) = "black" {}
        [HideInInspector] _UWVertexIndex ("Palette index on the vertex", Float) = 0

        // Which side is culled: 0 none, 1 the front, 2 the back.
        // Default none - everything so far is drawn double-sided. Only the portcullis
        // sets a value, and UWLevelLoader decides which one: it culls the BACK face (2),
        // so exactly the face turned towards the viewer remains. (Checked 2026-09-16: this
        // comment used to claim the opposite, 1; the loader and its image comparison with
        // the original from 2026-09-05 are the ones that count.)
        [HideInInspector] _Cull ("Cull", Float) = 0

        // The floor of the model's own tile, per renderer (UWObjectSpawner) - its origin may lie
        // below it, as the shrine's does. Only read with the keyword _UW_OWN_TILE, which only the
        // model face material carries - see UWOwnTile.hlsl.
        [HideInInspector] _UWModelFloorY ("Model Floor", Float) = -100000
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "TransparentCutout"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "AlphaTest"
        }
        LOD 200

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex UWVertex
            #pragma fragment UWFragment
            #pragma target 3.0

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            // Forward+ (see UWRemasterSetup): lights are looked up per screen cluster
            // instead of per object. Without it a whole chunk of 16 by 16 tiles would get
            // at most four additional lights.
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #pragma multi_compile_local_fragment _ _UW_OWN_TILE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "UWOwnTile.hlsl"
            #include "UWHallucination.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            // Point sampled through URP's shared sampler: an index must not be filtered.
            TEXTURE2D(_IndexTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _Cutoff;
                float _AmbientFloor;
                float _PointLightCap;
                float _Cull;
                float _UWModelFloorY;
                float _UWVertexIndex;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                float2 paletteData : TEXCOORD1;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
                float  fogFactor  : TEXCOORD3;
                float4 color      : TEXCOORD4;
                float2 paletteData : TEXCOORD5;
            };

            Varyings UWVertex(Attributes IN)
            {
                Varyings OUT = (Varyings)0;

                VertexPositionInputs positions = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs normals = GetVertexNormalInputs(IN.normalOS);

                OUT.positionCS = positions.positionCS;
                OUT.positionWS = positions.positionWS;
                OUT.normalWS = normals.normalWS;
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.fogFactor = ComputeFogFactor(positions.positionCS.z);
                // If the mesh has no vertex colour channel (most objects), Unity
                // supplies white here automatically - so the multiply has no effect. Baked-in
                // 3D models set the actual palette colour here for single-colour
                // surfaces, see UWObjectSpawner.fSpawn3DModel.
                OUT.color = IN.color;
                OUT.paletteData = IN.paletteData;

                return OUT;
            }

            // pbFrontFace tells which side of the surface is currently being drawn.
            half4 UWFragment(Varyings IN, bool pbFrontFace : SV_IsFrontFace
                #if defined(_UW_OWN_TILE)
                    , out float outDepth : SV_Depth
                #endif
                ) : SV_Target
            {
                #if defined(_UW_OWN_TILE)
                    // Only the side facing the viewer - see UWOwnTile.hlsl on the back of a model.
                    outDepth = pbFrontFace
                        ? UWOwnTileDepth(IN.positionWS, TransformObjectToWorld(float3(0, 0, 0)), _UWModelFloorY, IN.positionCS.z)
                        : IN.positionCS.z;
                #endif

                half4 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);

                clip(albedo.a - _Cutoff);

                // THE HALLUCINATION (UWHallucination.hlsl), where the palette index is known: from the
                // vertex on the model faces, else from the index atlas.
                half3 lOTint = IN.color.rgb;
                float lfIndex = -1.0;
                float lfExtraLevel = 0.0;

                if (_UWVertexIndex > 0.5)
                {
                    lfIndex = floor(IN.paletteData.x + 0.5);
                    lfExtraLevel = max(IN.paletteData.y - 1.0, 0.0);
                }
                else
                {
                    half4 lOIndexTexel = SAMPLE_TEXTURE2D(_IndexTex, sampler_PointClamp, IN.uv);

                    if (lOIndexTexel.a > 0.0h)
                        lfIndex = UWToIndex(lOIndexTexel.r);
                }

                if (lfIndex >= 0.0)
                {
                    if (UWRemasterLightEffect())
                        return half4(UWRemasterLightEffectColour(lfIndex, 0.0, IN.positionWS, lfExtraLevel, IN.positionCS.xy), 1.0h);

                    if (UWRemasterPaletteSwapped())
                    {
                        albedo.rgb = UWRemasterPaletteColour(lfIndex);
                        lOTint = half3(1.0h, 1.0h, 1.0h);
                    }
                }

                // DOUBLE-SIDED LIGHTING. Drawing here is double-sided by default (_Cull 0), and
                // a surface seen from behind has a normal pointing away from the viewer
                // - the dot product with the light direction becomes zero and only the
                // ambient floor remains. On the portcullis, whose bars consist of two
                // opposite-facing surfaces, the back one was therefore clearly too
                // dark (per user, 2026-09-05).
                //
                // Taking into account which side is being drawn costs nothing and is also
                // correct for every other double-sided surface. The palette renderer does not
                // have the problem, it knows no normals.
                float3 normalWS = normalize(IN.normalWS) * (pbFrontFace ? 1.0 : -1.0);

                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                half3 lighting = mainLight.color * (saturate(dot(normalWS, mainLight.direction)) * mainLight.shadowAttenuation * mainLight.distanceAttenuation);

                #if defined(_ADDITIONAL_LIGHTS)
                uint lightCount = GetAdditionalLightsCount();

                // The Forward+ cluster light loop finds its lights via the
                // screen position and expects an InputData named inputData for that.
                InputData inputData = (InputData)0;
                inputData.positionWS = IN.positionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionCS);
                // Point lights are physically computed with 1/distance squared. If the
                // player stands close to a wall, this goes towards infinity and the surface
                // burns out to white. The contribution per light is therefore capped - at normal
                // distance this has no effect, only the close range is damped.
                LIGHT_LOOP_BEGIN(lightCount)
                    Light light = GetAdditionalLight(lightIndex, IN.positionWS, half4(1, 1, 1, 1));
                    half3 pointContribution = light.color * (saturate(dot(normalWS, light.direction)) * light.shadowAttenuation * light.distanceAttenuation);
                    // A per-channel min() would cap red first, then green, then blue, and at
                    // high raw intensity pull all three to the same value - warm
                    // torch light would turn grey. Instead the whole colour vector is scaled down
                    // uniformly as soon as the brightest channel exceeds the limit; the
                    // ratio between the channels, and thus the light colour, is preserved.
                    half lfPeakChannel = max(pointContribution.r, max(pointContribution.g, pointContribution.b));
                    half lfCapScale = lfPeakChannel > _PointLightCap ? (_PointLightCap / lfPeakChannel) : 1.0h;
                    lighting += pointContribution * lfCapScale;
                LIGHT_LOOP_END
                #endif

                lighting += SampleSH(normalWS) + _AmbientFloor.xxx;

                half3 color = albedo.rgb * lighting * lOTint;
                color = MixFog(color, IN.fogFactor);

                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
