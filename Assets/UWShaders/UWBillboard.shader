// Shader for the object sprites in the dungeon.
//
// Facing the camera happens here in the vertex shader. Previously this was done by a
// billboard MonoBehaviour whose Update() queried Camera.main for every single object in every frame
// and computed a LookAt.
//
// The mesh lies in object space with the pivot at the origin: x is the sideways
// offset, y the height above the floor, z stays unused.
Shader "UW/Billboard"
{
    Properties
    {
        _MainTex ("Object atlas", 2D) = "white" {}

        // The index version of the atlas (UWObjectAtlasBuilder, UWCritterAtlasBuilder: the palette
        // index in red, a translucent pixel's table plus one in green, the opacity in alpha). Read
        // for the hallucination's pictures and the ghosts only (UWHallucination.hlsl); black, i.e.
        // nothing, where a material has none.
        _IndexTex ("Index atlas", 2D) = "black" {}
        _Cutoff ("Alpha cutoff", Range(0, 1)) = 0.5
        _AmbientFloor ("Ambient floor", Range(0, 1)) = 0.08
        _PointLightCap ("Point light cap (close range)", Range(0.3, 5)) = 1.4
        _SpriteGlow ("Self glow (Remastered, per renderer)", Range(0, 4)) = 0
        _GlowThreshold ("Glow threshold", Range(0, 1)) = 0.6

        // Two sprites at the same tile spot lie exactly on top of each other and fight over
        // depth - the fountain, for example, consists of the basin (object 302) and the
        // animated water (457), which the original draws in front of it. This value
        // pushes the sprite along the view direction towards the camera without changing its position in
        // the world.
        _DepthBias ("Offset towards camera (world units)", Float) = 0

        // Set for creatures (UWCritterAnimator) and items (UWLevelLoader), see UWOwnTile.hlsl.
        [Toggle(_UW_OWN_TILE)] _OwnTile ("Own tile does not cover", Float) = 0
        [HideInInspector] _BigRadius ("A big object's radius in eighths (creatures), 0 for items", Float) = 0

        // Translucent sprites (mist, ghosts - see UWTransparencyTables) need
        // real blending instead of the hard alpha cutoff. So that no second shader
        // is needed for this, the blend state is a material property: opaque is
        // One/Zero with depth write, translucent SrcAlpha/OneMinusSrcAlpha without.
        [HideInInspector] _SrcBlend ("Source blend", Float) = 1
        [HideInInspector] _DstBlend ("Destination blend", Float) = 0
        [HideInInspector] _ZWrite ("Depth write", Float) = 1
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

            Cull Off
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]

            HLSLPROGRAM
            #pragma vertex UWVertex
            #pragma fragment UWFragment
            #pragma target 3.0
            #pragma multi_compile_local _ _UW_OWN_TILE

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            // Forward+ (see UWRemasterSetup): lights are looked up per screen cluster
            // instead of per object. Without it a whole chunk of 16 by 16 tiles would get
            // at most four additional lights.
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "UWOwnTile.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
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
                float _SpriteGlow;
                float _GlowThreshold;
                float _DepthBias;
                float _OwnTile;
                float _BigRadius;
                float _SrcBlend;
                float _DstBlend;
                float _ZWrite;
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
                float3 normalWS   : TEXCOORD2;
                float  fogFactor  : TEXCOORD3;
                float3 pivotWS    : TEXCOORD4;
            };

            Varyings UWVertex(Attributes IN)
            {
                Varyings OUT = (Varyings)0;

                // Pivot of the object in the world.
                float3 pivotWS = TransformObjectToWorld(float3(0, 0, 0));

                // Rotate around the vertical axis only - the sprites stand upright in space.
                // PARALLEL TO THE SCREEN, as the original draws a sprite - a flat picture scaled by its
                // distance (per user, 2026-09-28: sprites still stabbed through one another). Turned to the
                // camera POSITION, two sprites close together stood at an angle and could cross.
                float3 toCamera = UNITY_MATRIX_V[2].xyz;
                toCamera.y = 0;

                float3 forward = normalize(toCamera + float3(0, 0, 1e-4));

                // cross(forward, up) and not cross(up, forward): what we want is the
                // camera's right vector. The other way round it points left and the
                // graphic appears mirrored horizontally.
                float3 right = normalize(cross(forward, float3(0, 1, 0)));

                float3 positionWS = pivotWS + right * IN.positionOS.x + float3(0, 1, 0) * IN.positionOS.y + forward * _DepthBias;

                OUT.positionWS = positionWS;
                OUT.pivotWS = pivotWS;
                OUT.positionCS = TransformWorldToHClip(positionWS);
                OUT.normalWS = forward;
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.fogFactor = ComputeFogFactor(OUT.positionCS.z);

                return OUT;
            }

            #if defined(_UW_OWN_TILE)
            half4 UWFragment(Varyings IN, out float outDepth : SV_Depth) : SV_Target
            #else
            half4 UWFragment(Varyings IN) : SV_Target
            #endif
            {
                half4 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);

                clip(albedo.a - _Cutoff);

                #if defined(_UW_OWN_TILE)
                outDepth = UWOwnTileSpriteDepth(IN.positionWS, IN.pivotWS, IN.pivotWS.y, IN.positionCS.z, _BigRadius);
                #endif

                // THE HALLUCINATION AND THE GHOSTS (UWHallucination.hlsl), where the atlas has an
                // index version. Shaded by the PIVOT's distance, as the palette path shades a sprite.
                half4 lOIndexTexel = SAMPLE_TEXTURE2D(_IndexTex, sampler_PointClamp, IN.uv);

                if (lOIndexTexel.a > 0.0h)
                {
                    float lfTable = UWRemasterXferTable(lOIndexTexel);

                    if (lfTable >= 0.0)
                    {
                        // A translucent pixel replaces the picture behind it through its table,
                        // at full opacity - the mixing is in the table. Without the lookups (the
                        // hallucination's palette) the fitted blend below stays.
                        if (UWRemasterLightEffect())
                            return half4(UWRemasterXferUnderLightEffect(lfTable, IN.pivotWS, IN.positionCS.xy), 1.0h);

                        if (UWRemasterXferReady())
                        {
                            half3 lOBehind = LOAD_TEXTURE2D_X(_CameraOpaqueTexture, uint2(IN.positionCS.xy)).rgb;

                            return half4(UWRemasterXferColour(lfTable, lOBehind), 1.0h);
                        }
                    }
                    else
                    {
                        float lfIndex = UWToIndex(lOIndexTexel.r);

                        if (UWRemasterLightEffect())
                            return half4(UWRemasterLightEffectColour(lfIndex, 0.0, IN.pivotWS, 0.0, IN.positionCS.xy), albedo.a);

                        if (UWRemasterPaletteSwapped())
                            albedo.rgb = UWRemasterPaletteColour(lfIndex);
                    }
                }

                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                half3 lighting = mainLight.color * (saturate(dot(IN.normalWS, mainLight.direction)) * mainLight.shadowAttenuation * mainLight.distanceAttenuation);

                #if defined(_ADDITIONAL_LIGHTS)
                uint lightCount = GetAdditionalLightsCount();

                // The Forward+ cluster light loop finds its lights via the
                // screen position and expects an InputData named inputData for that.
                InputData inputData = (InputData)0;
                inputData.positionWS = IN.positionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionCS);
                // Point lights physically use 1/distance squared. Close to the
                // light source that goes towards infinity - so the contribution per light is
                // capped; at normal distances this has no effect.
                LIGHT_LOOP_BEGIN(lightCount)
                    Light light = GetAdditionalLight(lightIndex, IN.positionWS, half4(1, 1, 1, 1));
                    half3 pointContribution = light.color * (saturate(dot(IN.normalWS, light.direction)) * light.shadowAttenuation * light.distanceAttenuation);
                    // A per-channel min() would cap first red, then green, then blue and at
                    // high raw intensity pull all three to the same value - warm
                    // torch light would turn grey. Instead the whole colour vector is scaled down
                    // uniformly as soon as the brightest channel exceeds the limit; the
                    // ratio between the channels, and with it the light colour, is preserved.
                    half lfPeakChannel = max(pointContribution.r, max(pointContribution.g, pointContribution.b));
                    half lfCapScale = lfPeakChannel > _PointLightCap ? (_PointLightCap / lfPeakChannel) : 1.0h;
                    lighting += pointContribution * lfCapScale;
                LIGHT_LOOP_END
                #endif

                lighting += SampleSH(IN.normalWS) + _AmbientFloor.xxx;

                half3 color = albedo.rgb * lighting;

                // GLOW FOR SPRITES (Remastered render mode). It is set per renderer
                // by UWRemasterLights, and only on light source objects; otherwise it stays
                // zero and changes nothing. Which pixels glow is determined by their BRIGHTNESS,
                // not the palette: only the campfire is painted in lava colours, torch,
                // candle, lantern, glow stone, mushroom and orbs are not (counted with
                // UWLightObjectDump). The flame of a torch is bright, its handle dark -
                // the threshold separates the two.
                //
                // Measured on the BRIGHTEST COLOUR CHANNEL, not on luminance: a red flame
                // (255, 60, 0) has a luminance below 0.45 and fell through the threshold - in
                // the test image the glow at the campfire changed exactly five bytes. The brightest
                // channel is full for every saturated flame, but not for grey stone and dark
                // wood.
                half lfValue = max(albedo.r, max(albedo.g, albedo.b));
                half lfGlowMask = saturate((lfValue - _GlowThreshold) / max(0.001h, 1.0h - _GlowThreshold));
                color += albedo.rgb * (lfGlowMask * _SpriteGlow);
                color = MixFog(color, IN.fogFactor);

                return half4(color, albedo.a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite [_ZWrite]
            ColorMask R
            Cull Off

            HLSLPROGRAM
            #pragma vertex UWDepthVertex
            #pragma fragment UWDepthFragment
            #pragma target 3.0
            #pragma multi_compile_local _ _UW_OWN_TILE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "UWOwnTile.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _Cutoff;
                float _AmbientFloor;
                float _PointLightCap;
                float _SpriteGlow;
                float _GlowThreshold;
                float _DepthBias;
                float _OwnTile;
                float _BigRadius;
                float _SrcBlend;
                float _DstBlend;
                float _ZWrite;
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
                float3 pivotWS    : TEXCOORD2;
            };

            Varyings UWDepthVertex(Attributes IN)
            {
                Varyings OUT = (Varyings)0;

                float3 pivotWS = TransformObjectToWorld(float3(0, 0, 0));
                // PARALLEL TO THE SCREEN, as the original draws a sprite - a flat picture scaled by its
                // distance (per user, 2026-09-28: sprites still stabbed through one another). Turned to the
                // camera POSITION, two sprites close together stood at an angle and could cross.
                float3 toCamera = UNITY_MATRIX_V[2].xyz;
                toCamera.y = 0;

                float3 forward = normalize(toCamera + float3(0, 0, 1e-4));

                // cross(forward, up) and not cross(up, forward): what we want is the
                // camera's right vector. The other way round it points left and the
                // graphic appears mirrored horizontally.
                float3 right = normalize(cross(forward, float3(0, 1, 0)));

                float3 positionWS = pivotWS + right * IN.positionOS.x + float3(0, 1, 0) * IN.positionOS.y + forward * _DepthBias;

                OUT.positionWS = positionWS;
                OUT.pivotWS = pivotWS;
                OUT.positionCS = TransformWorldToHClip(positionWS);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);

                return OUT;
            }

            #if defined(_UW_OWN_TILE)
            half4 UWDepthFragment(Varyings IN, out float outDepth : SV_Depth) : SV_Target
            #else
            half4 UWDepthFragment(Varyings IN) : SV_Target
            #endif
            {
                half alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv).a;
                clip(alpha - _Cutoff);

                #if defined(_UW_OWN_TILE)
                outDepth = UWOwnTileSpriteDepth(IN.positionWS, IN.pivotWS, IN.pivotWS.y, IN.positionCS.z, _BigRadius);
                #endif

                return 0;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
