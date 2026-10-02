// Geometry shader for the dungeon's floor, ceiling and walls.
//
// All level textures live in one Texture2DArray instead of thousands of
// single textures. Deliberately no atlas: the walls scale their UVs beyond 1
// to tile the texture over the wall height. In an atlas that would bleed
// into the neighbouring texture; per array slice, Repeat works normally.
//
// UV channel 0 is float3: xy = texture coordinate, z = slice index.
//
// TWO MODES in one shader, switched via the keyword
// _UW_REMASTER (see UWRemasterRenderer):
//
//   off - the plain URP path: the finished texture colour, lit by URP.
//   on  - "Remastered": the same colour, plus relief from the normal array and a
//         specular highlight. The pixels stay hard as in the original, only the light is modern.
Shader "UW/Dungeon"
{
    Properties
    {
        _TexArray ("Level textures", 2DArray) = "" {}
        _NormalArray ("Normals (Remastered)", 2DArray) = "" {}
        _IndexArray ("Palette indices (Remastered)", 2DArray) = "" {}
        _MaterialLut ("Surface table (Remastered)", 2D) = "" {}
        _AmbientFloor ("Base brightness", Range(0, 1)) = 0.08
        _PointLightCap ("Point light cap (close range)", Range(0.3, 5)) = 1.4
        _NormalStrength ("Normal strength", Range(0, 4)) = 1
        _SpecularStrength ("Specular strength", Range(0, 2)) = 0.25
        _SpecularPower ("Specular sharpness", Range(1, 128)) = 24
        _Roughness ("Roughness", Range(0, 2)) = 1
        _SpecularBase ("Base reflectance", Range(0.01, 0.4)) = 0.06
        _WrapLighting ("Soft light wrap", Range(0, 1)) = 0.35
        _EmissiveStrength ("Emissive strength (lava)", Range(0, 4)) = 1.5
        _ParallaxDepth ("Parallax depth", Range(0, 0.2)) = 0.03
        _ParallaxLevels ("Parallax levels", Range(1, 16)) = 4
        _ParallaxSteps ("Parallax steps", Range(4, 48)) = 16
        _ParallaxSnap ("Parallax snapped to pixel grid", Range(0, 1)) = 1
        _SelfShadowStrength ("Self shadow", Range(0, 1)) = 0
        _SelfShadowDepth ("Self shadow relief depth", Range(0.001, 0.1)) = 0.02
        _SelfShadowReach ("Self shadow reach", Range(0.01, 0.3)) = 0.1
        _SelfShadowSoftness ("Self shadow softness", Range(0.5, 16)) = 4
        _CavityStrength ("Occlusion from height", Range(0, 1)) = 0
        _ParallaxSign ("Parallax direction, -1 inverts", Range(-1, 1)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }
        LOD 200

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex UWVertex
            #pragma fragment UWFragment
            #pragma target 3.5
            #pragma require 2darray

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            // Forward+ (see UWRemasterSetup): lights are looked up per screen cluster
            // instead of per object. Without it a whole chunk of 16 by 16 tiles would get
            // at most four additional lights.
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            // Ambient occlusion (SSAO feature in UWUniversalRenderer). Without this variant
            // URP does compute the occlusion, but no pixel reads it - on and off
            // looked the same (per user, 2026-09-13).
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION

            // multi_compile and not shader_feature: the material is created at runtime,
            // a shader_feature variant would be stripped from the built game.
            #pragma multi_compile_local_fragment _ _UW_REMASTER

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "UWHallucination.hlsl"

            TEXTURE2D_ARRAY(_TexArray);
            SAMPLER(sampler_TexArray);

            TEXTURE2D_ARRAY(_NormalArray);
            SAMPLER(sampler_NormalArray);

            TEXTURE2D_ARRAY(_IndexArray);
            SAMPLER(sampler_IndexArray);

            TEXTURE2D(_MaterialLut);
            SAMPLER(sampler_MaterialLut);

            CBUFFER_START(UnityPerMaterial)
                float _AmbientFloor;
                float _PointLightCap;
                float _NormalStrength;
                float _SpecularStrength;
                float _SpecularPower;
                float _Roughness;
                float _SpecularBase;
                float _WrapLighting;
                float _EmissiveStrength;
                float _ParallaxDepth;
                float _ParallaxLevels;
                float _ParallaxSteps;
                float _ParallaxSnap;
                float _SelfShadowStrength;
                float _SelfShadowDepth;
                float _SelfShadowReach;
                float _SelfShadowSoftness;
                float _CavityStrength;
                float _ParallaxSign;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float3 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 uv         : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float  fogFactor  : TEXCOORD3;
            };

            // The tangent frame is built PER PIXEL from the derivatives of position
            // and texture coordinate (Mikkelsen's method). The chunk meshes carry
            // no tangents - that answers the open question from the plan, and
            // without touching the geometry. It is also the more robust way here:
            // slopes and diagonal tiles would otherwise have needed their own special cases.
            float3x3 UWBuildTangentFrame(float3 pONormalWS, float3 pOPositionWS, float2 pOUv)
            {
                float3 lODeltaPosX = ddx(pOPositionWS);
                float3 lODeltaPosY = ddy(pOPositionWS);
                float2 lODeltaUvX = ddx(pOUv);
                float2 lODeltaUvY = ddy(pOUv);

                float3 lOPerpY = cross(lODeltaPosY, pONormalWS);
                float3 lOPerpX = cross(pONormalWS, lODeltaPosX);

                float3 lOTangent = (lOPerpY * lODeltaUvX.x) + (lOPerpX * lODeltaUvY.x);
                float3 lOBitangent = (lOPerpY * lODeltaUvX.y) + (lOPerpX * lODeltaUvY.y);

                float lfScale = rsqrt(max(1e-8, max(dot(lOTangent, lOTangent), dot(lOBitangent, lOBitangent))));

                // HANDEDNESS OF THE SCREEN. The derivation assumes that ddx cross ddy
                // points along the normal (y up). Under Direct3D y points
                // down, and Unity flips once more when rendering into textures - then
                // tangent AND bitangent flip. Parallax, normal mapping and self shadow were
                // inverted by this: the joints bulged outwards (per user, 2026-09-13,
                // "Definitely" on the comparison image). With relief alone this is hardly noticeable,
                // the eye reads light and dark consistently the other way round too.
                lfScale *= dot(cross(lODeltaPosX, lODeltaPosY), pONormalWS) < 0.0 ? -1.0 : 1.0;

                return float3x3(lOTangent * lfScale, lOBitangent * lfScale, pONormalWS);
            }

            /// Texels per slice in all arrays, see UWTextureArrayBuilder.SliceResolution.
            #define UW_SLICE_RESOLUTION 64.0

            /// Which mipmap the height for snapping comes from, see UWSampleDepth.
            #define UW_PARALLAX_SNAP_MIP 2.0

            /// The height of a pixel is stored in the alpha channel of the normal array (from the
            /// detected joints, see UWTextureArrayBuilder.BuildNormals and UWHeightMapBuilder). Here it
            /// becomes the DEPTH - 0 is the surface, 1 the deepest point - rounded to
            /// a few levels.
            float UWSampleDepth(float2 pOUv, float pfSlice)
            {
                // On the pixel grid the height is read from a COARSE mipmap (4 by 4
                // texels averaged). With the fine height it varies from texel to texel, the
                // rounded offset jumped back and forth between neighbouring pixels, and the
                // stones got horizontal streaks and doubled pixel rows (rendered
                // with a render test tool, 2026-09-13). Read coarsely, the same offset applies
                // across a whole stone.
                float lfHeight = _ParallaxSnap > 0.5
                    ? SAMPLE_TEXTURE2D_ARRAY_LOD(_NormalArray, sampler_NormalArray, pOUv, pfSlice, UW_PARALLAX_SNAP_MIP).a
                    : SAMPLE_TEXTURE2D_ARRAY(_NormalArray, sampler_NormalArray, pOUv, pfSlice).a;
                float lfLevels = max(1.0, _ParallaxLevels);

                // STEPPED HEIGHT, as in the plan: not a smooth landscape, but a few
                // flat layers. That suits the hard pixels and makes the image look calmer
                // in motion than a continuous relief.
                return 1.0 - (floor(lfHeight * lfLevels) / lfLevels);
            }

            /// Parallax occlusion mapping: the view ray runs across the surface in tangent space
            /// until it dips below the height map. The spot hit there is
            /// drawn - so joints and stones shift against each other when you
            /// move, and the wall gains depth without adding a single triangle.
            float2 UWParallaxUv(float2 pOUv, float pfSlice, float3 pOViewTS)
            {
                // At grazing view angles the ray runs far across the surface and picks
                // pixels far off - the wall smears, and dark spots
                // turn into black holes. Torch light from the side makes this clearly
                // visible. So the depth is faded out there.
                float lfDepth = _ParallaxDepth * smoothstep(0.15, 0.6, abs(pOViewTS.z));

                if (lfDepth <= 0.0001)
                    return pOUv;

                // Looking at the surface at a grazing angle needs more steps than head-on.
                float lfSteps = max(4.0, _ParallaxSteps * lerp(1.5, 0.6, saturate(abs(pOViewTS.z))));
                float lfStep = 1.0 / lfSteps;

                // The ray must not run off to infinity when looking almost parallel to the
                // wall - hence the lower bound for the Z component.
                // _ParallaxSign stays as a comparison switch. The inversion the user
                // saw was not here, but in the handedness in UWBuildTangentFrame.
                float2 lOOffset = (pOViewTS.xy / max(0.35, abs(pOViewTS.z))) * lfDepth * lfStep * _ParallaxSign;

                float lfRayDepth = 0.0;
                float2 lOAt = pOUv;
                float lfMapDepth = UWSampleDepth(lOAt, pfSlice);

                [loop]
                for (int liStep = 0; liStep < 48; liStep++)
                {
                    if (lfRayDepth >= lfMapDepth || (float)liStep >= lfSteps)
                        break;

                    lOAt -= lOOffset;
                    lfRayDepth += lfStep;
                    lfMapDepth = UWSampleDepth(lOAt, pfSlice);
                }

                // SNAP TO THE PIXEL GRID (per user, 2026-09-13): shifted by fractions of a
                // texel, the hard pixels swam and distorted while moving.
                // Rounded to whole texels every pixel stays intact - the surface jumps
                // texel by texel, which suits pixel art. Colour, normal and
                // palette index read the same shifted spot.
                if (_ParallaxSnap > 0.5)
                    return pOUv + (round((lOAt - pOUv) * UW_SLICE_RESOLUTION) / UW_SLICE_RESOLUTION);

                return lOAt;
            }

            /// SELF SHADOW from the height map (per user, 2026-09-13). The eye perceives depth
            /// mostly through shadows: a joint looks deep when the stone next to it casts a shadow
            /// into it, and the shadow moves with the torch.
            ///
            /// From the pixel a ray runs across the height map towards the light and
            /// rises as steeply as the light falls in. If something rises above the
            /// ray along the way, the point is in shadow - the higher it rises and the closer it is,
            /// the darker. UNLIKE PARALLAX this shifts no texture coordinate: nothing
            /// swims or jumps.
            ///
            /// The height is 0 to 1 and stands for _SelfShadowDepth texture units of relief.
            /// Sampling uses the pixel's derivatives, so that in the distance the
            /// averaged mipmap applies and the shadows fade out calmly there.
            half UWSelfShadow(float2 pOUv, float pfSlice, float3 pOLightTS, float2 pODdx, float2 pODdy)
            {
                if (_SelfShadowStrength <= 0.0 || pOLightTS.z <= 0.001)
                    return 1.0h;

                float lfFlat = length(pOLightTS.xy);

                if (lfFlat < 0.0001)
                    return 1.0h;

                float2 lODirection = pOLightTS.xy / lfFlat;
                // Rise of the ray per texture unit, measured in height 0 to 1.
                float lfRise = (pOLightTS.z / lfFlat) / _SelfShadowDepth;
                float lfStart = SAMPLE_TEXTURE2D_ARRAY_GRAD(_NormalArray, sampler_NormalArray, pOUv, pfSlice, pODdx, pODdy).a;

                // The ray need not run further than to the highest possible height.
                float lfDistance = min(_SelfShadowReach, (1.0 - lfStart) / max(lfRise, 0.0001));

                if (lfDistance <= 0.0)
                    return 1.0h;

                float lfOcclusion = 0.0;

                [loop]
                for (int liStep = 1; liStep <= 8; liStep++)
                {
                    float lfT = liStep / 8.0;
                    float lfAlong = lfDistance * lfT;
                    float lfHeight = SAMPLE_TEXTURE2D_ARRAY_GRAD(_NormalArray, sampler_NormalArray,
                        pOUv + (lODirection * lfAlong), pfSlice, pODdx, pODdy).a;
                    float lfAbove = lfHeight - (lfStart + (lfAlong * lfRise));

                    // Near obstacles count fully, distant ones less - that gives a soft
                    // falloff instead of a hard shadow edge.
                    lfOcclusion = max(lfOcclusion, lfAbove * (1.0 - (lfT * 0.5)));
                }

                return 1.0h - (saturate(lfOcclusion * _SelfShadowSoftness) * _SelfShadowStrength);
            }

            Varyings UWVertex(Attributes IN)
            {
                Varyings OUT = (Varyings)0;

                VertexPositionInputs positions = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs normals = GetVertexNormalInputs(IN.normalOS);

                OUT.positionCS = positions.positionCS;
                OUT.positionWS = positions.positionWS;
                OUT.normalWS = normals.normalWS;
                OUT.uv = IN.uv;
                OUT.fogFactor = ComputeFogFactor(positions.positionCS.z);

                return OUT;
            }

            half4 UWFragment(Varyings IN) : SV_Target
            {
                float3 normalWS = normalize(IN.normalWS);
                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(IN.positionWS);
                half3 specular = half3(0, 0, 0);
                half lfGloss = 1.0h;
                half lfGlow = 0.0h;
                half lfRough = 1.0h;
                float2 lOUv = IN.uv.xy;
                half lfCavity = 1.0h;

                #if defined(_UW_REMASTER)
                // The tangent frame is needed twice: for the parallax view ray
                // and for the normal.
                float3x3 lOFrame = UWBuildTangentFrame(normalWS, IN.positionWS, IN.uv.xy);
                float2 lOUvDdx = ddx(IN.uv.xy);
                float2 lOUvDdy = ddy(IN.uv.xy);

                // No parallax on lava: its brightness is glow, not shape - the
                // height map turned it into bubbling bumps. Which material it is, is told by the
                // palette index at the unshifted spot.
                half lfIndexHere = SAMPLE_TEXTURE2D_ARRAY(_IndexArray, sampler_IndexArray, IN.uv.xy, IN.uv.z).r;
                half lfGlowHere = SAMPLE_TEXTURE2D(_MaterialLut, sampler_MaterialLut,
                    float2(((lfIndexHere * 255.0) + 0.5) / 256.0, 0.5)).g;

                if (lfGlowHere < 0.5h)
                    lOUv = UWParallaxUv(IN.uv.xy, IN.uv.z, mul(lOFrame, viewDirWS));

                // THE HALLUCINATION'S SCRAMBLED MAPPER (UWHallucination.hlsl), after the parallax
                // like the palette path's after its plain mapping. Colour, index and normal all read
                // the scrambled texel, with the derivatives of the unscrambled coordinate: the
                // scrambled one jumps from pixel to pixel and would pick the smallest mipmap. With the
                // effect off they are the derivatives the plain sample takes anyway.
                float2 lOSampleDdx = ddx(lOUv);
                float2 lOSampleDdy = ddy(lOUv);

                if (UWScrambleActive())
                    lOUv = UWScrambleUV(float3(lOUv, IN.uv.z));

                half4 albedo = SAMPLE_TEXTURE2D_ARRAY_GRAD(_TexArray, sampler_TexArray, lOUv, IN.uv.z,
                    lOSampleDdx, lOSampleDdy);
                #else
                half4 albedo = SAMPLE_TEXTURE2D_ARRAY(_TexArray, sampler_TexArray, lOUv, IN.uv.z);
                #endif

                #if defined(_UW_REMASTER)
                // WHAT MATERIAL THIS IS, is told by the pixel's palette index: the
                // surface table (UWTextureArrayBuilder.BuildMaterialLookup) holds gloss and
                // glow for each of the 256 indices. Lava (16-23) and water (48-63)
                // are the two ranges in use.
                half lfIndex = SAMPLE_TEXTURE2D_ARRAY_GRAD(_IndexArray, sampler_IndexArray, lOUv, IN.uv.z,
                    lOSampleDdx, lOSampleDdy).r;

                // THE HALLUCINATION'S OTHER TWO PICTURES (UWHallucination.hlsl): under the light
                // table the surface as the palette path draws it, unlit; under another palette the
                // index's colour in that palette, lit as usual.
                if (UWRemasterLightEffect())
                {
                    return half4(UWRemasterLightEffectColour(UWToIndex(lfIndex),
                        UWDungeonSurfacePart(IN.uv.z, IN.positionWS.y), IN.positionWS, 0.0, IN.positionCS.xy), 1.0h);
                }

                if (UWRemasterPaletteSwapped())
                    albedo.rgb = UWRemasterPaletteColour(UWToIndex(lfIndex));

                // The index comes out of the R8 array as 0 to 1 (i.e. index divided by 255) and
                // must land on the centre of its entry in the 256 wide table: otherwise
                // the upper indices pick one entry off.
                float lfLookup = ((lfIndex * 255.0) + 0.5) / 256.0;
                half4 surface = SAMPLE_TEXTURE2D(_MaterialLut, sampler_MaterialLut, float2(lfLookup, 0.5));

                lfGloss = surface.r * 2.0h;
                lfGlow = surface.g;

                // The normals come from the height map of the texture (UWTextureArrayBuilder.
                // BuildNormals) and live in the same slice as the colour.
                half4 packed = SAMPLE_TEXTURE2D_ARRAY_GRAD(_NormalArray, sampler_NormalArray, lOUv, IN.uv.z,
                    lOSampleDdx, lOSampleDdy);

                // The blue channel carries the roughness, not the Z of the normal - that follows from
                // X and Y, because the normal always points out of the surface.
                lfRough = saturate(packed.b * _Roughness);

                float3 tangentNormal;

                tangentNormal.xy = (packed.rg * 2.0) - 1.0;
                tangentNormal.z = sqrt(saturate(1.0 - dot(tangentNormal.xy, tangentNormal.xy)));

                // On lava the brightness is the GLOW, not the shape - a relief from it
                // would be wrong. So the normal is kept flat there.
                tangentNormal.xy *= _NormalStrength * (1.0h - lfGlow);
                tangentNormal = normalize(tangentNormal);

                normalWS = normalize(mul(tangentNormal, lOFrame));

                // OCCLUSION FROM HEIGHT (per user, 2026-09-13: self shadow "less
                // pronounced than hoped"). The player carries the light almost at eye level, and then
                // every shadow falls exactly behind the stone that casts it. Whatever lies lower
                // than its surroundings - joint edges, recessed stones - therefore gets
                // permanently less light, wherever it comes from. The surroundings are the same
                // height three mipmap levels coarser (eight by eight texels), read relative to the
                // pixel's own level, so that it does not flicker in the distance. Not on lava.
                half lfAround = SAMPLE_TEXTURE2D_ARRAY_GRAD(_NormalArray, sampler_NormalArray, lOUv, IN.uv.z,
                    lOUvDdx * 8.0, lOUvDdy * 8.0).a;
                lfCavity = lerp(1.0h, saturate(1.0h - (max(0.0h, lfAround - packed.a) * 3.0h)),
                    _CavityStrength * (1.0h - lfGlow));
                #endif

                // Occlusion from the SSAO pass. Outside Remastered or with the
                // feature disabled it is one and changes nothing.
                half lfDirectOcclusion = 1.0h;
                half lfIndirectOcclusion = 1.0h;

                #if defined(_UW_REMASTER)
                AmbientOcclusionFactor lOOcclusion = GetScreenSpaceAmbientOcclusion(GetNormalizedScreenSpaceUV(IN.positionCS));
                lfDirectOcclusion = lOOcclusion.directAmbientOcclusion;
                lfIndirectOcclusion = lOOcclusion.indirectAmbientOcclusion;
                #endif

                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                half3 lighting = mainLight.color * (saturate(dot(normalWS, mainLight.direction)) * mainLight.shadowAttenuation * mainLight.distanceAttenuation);

                #if defined(_ADDITIONAL_LIGHTS)
                uint lightCount = GetAdditionalLightsCount();

                // The Forward+ cluster light loop looks up its lights via the
                // screen position and expects an InputData named inputData for that.
                InputData inputData = (InputData)0;
                inputData.positionWS = IN.positionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionCS);
                LIGHT_LOOP_BEGIN(lightCount)
                    Light light = GetAdditionalLight(lightIndex, IN.positionWS, half4(1, 1, 1, 1));
                    half lfFacing = dot(normalWS, light.direction);

                    #if defined(_UW_REMASTER)
                    // SOFT TRANSITION to the side facing away. With pure Lambert every
                    // flank of the relief that faces away from the light turns pitch black at once - in
                    // reality scattered light fills it in. The wrap moves the
                    // boundary a little past the edge.
                    lfFacing = (lfFacing + _WrapLighting) / (1.0 + _WrapLighting);
                    #endif

                    half3 pointContribution = light.color * (saturate(lfFacing) * light.shadowAttenuation * light.distanceAttenuation);

                    #if defined(_UW_REMASTER)
                    // Only where the light arrives at all - the ray costs nine samples (start plus eight steps).
                    if (lfFacing > 0.0h && light.distanceAttenuation > 0.0001h)
                        pointContribution *= lerp(UWSelfShadow(lOUv, IN.uv.z, mul(lOFrame, light.direction),
                            lOUvDdx, lOUvDdy), 1.0h, lfGlow);
                    #endif
                    // Point lights physically use 1/distance-squared. If the
                    // player stands close to a wall, that goes towards infinity and the surface
                    // burns out to white. The contribution per light is therefore capped - at the usual
                    // distance this has no effect, only the close range is damped.
                    // A per-channel min() would clip red, then green, then blue and at
                    // high raw intensity pull all three to the same value - warm
                    // torch light would turn grey. Instead the whole colour vector is scaled down
                    // uniformly as soon as the brightest channel exceeds the limit; the
                    // ratio between the channels and thus the light colour is preserved.
                    half lfPeakChannel = max(pointContribution.r, max(pointContribution.g, pointContribution.b));

                    #if defined(_UW_REMASTER)
                    // SOFT cap instead of a hard one. The hard one sets every value above the
                    // limit to exactly the limit - near a torch ALL
                    // pixels end up at the same value, and the relief just gained is
                    // gone again (that is exactly what happened in the first test image). The soft curve
                    // (Reinhard) keeps differences and only approaches the
                    // limit; whatever still lies above one is caught by tonemapping.
                    half lfSoftLimit = _PointLightCap * 1.5h;
                    half lfCapScale = lfSoftLimit / (lfSoftLimit + lfPeakChannel);
                    #else
                    half lfCapScale = lfPeakChannel > _PointLightCap ? (_PointLightCap / lfPeakChannel) : 1.0h;
                    #endif

                    half3 lOCapped = pointContribution * lfCapScale;
                    lighting += lOCapped;

                    #if defined(_UW_REMASTER)
                    // A narrow highlight on damp stone - it is what makes the relief
                    // visible at all when the torch moves past it.
                    half3 halfVector = normalize(light.direction + viewDirWS);

                    // FRESNEL. Without it the highlight sat at full strength on every
                    // surface facing the player - and because his torch sits right at the eye,
                    // that was almost everything: it looked as if the whole corridor were wet
                    // (per user, 2026-09-12). A dielectric like stone reflects only
                    // about four percent head-on and only shines at grazing angles.
                    // 0.04 is the value for stone. Higher means it shines head-on too -
                    // up to polished or wet. The slider stays, because "correct" and
                    // "looks good" are not the same here: the torch sits at the eye,
                    // and physically correct the highlight is then almost invisible
                    // (per user, 2026-09-12: "now you can hardly see a difference").
                    half lfEdge = pow(1.0h - saturate(dot(viewDirWS, halfVector)), 5.0h);
                    half lfFresnel = _SpecularBase + ((1.0h - _SpecularBase) * lfEdge);

                    // Rough stone scatters broad and weak, smooth stone narrow and bright.
                    half lfSmooth = 1.0h - lfRough;
                    half lfPower = lerp(4.0h, _SpecularPower, lfSmooth * lfSmooth);

                    specular += lOCapped * (pow(saturate(dot(normalWS, halfVector)), lfPower)
                        * lfFresnel * lfSmooth * _SpecularStrength * lfGloss);
                    #endif
                LIGHT_LOOP_END
                #endif

                // Torches and mushrooms are the actual light sources later; the
                // base value only keeps unlit corners from being completely black.
                // In the dungeon almost all light comes from point lights, the base value is
                // tiny. If the occlusion only affected it, as URP intends for indirect
                // light, it would stay invisible - so it also hits the direct light,
                // with the feature's "Direct Lighting Strength".
                lighting *= lfDirectOcclusion * lfCavity;
                specular *= lfDirectOcclusion * lfCavity;
                lighting += (SampleSH(normalWS) + _AmbientFloor.xxx) * lfIndirectOcclusion * lfCavity;

                half3 color = (albedo.rgb * lighting) + specular;

                #if defined(_UW_REMASTER)
                // Lava glows by itself - so it is visible even in a dark
                // corridor, and bloom picks it up.
                color += albedo.rgb * (lfGlow * _EmissiveStrength);
                #endif
                color = MixFog(color, IN.fogFactor);

                return half4(color, 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma target 3.5
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
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
