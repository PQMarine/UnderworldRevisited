// THE PALETTE RENDERER'S SHADER for floor, ceiling and walls. In use when the render mode
// is Palette (see UWLevelAssembler.CreateMaterial and UWPaletteRenderPath);
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
            #include "UWPaletteEffects.hlsl"

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
                // Linear on the screen, as the original's fill steps it (UWCornerLevel).
                noperspective float cornerLevel : TEXCOORD2;
            };

            // smooth_over's length to the letter (Underworld Exhumed uw1 3d/SMOOTH.ASM): each axis in the
            // original's units (four to our one) shifted down by 5, squared and summed, and the root by FOUR
            // Newton steps from 64, in whole numbers. Four steps are not enough for a small number: a
            // length of 2 comes out 4 and 4 comes out 5, so on a near wall the corners differ where
            // the true root would give them all one level, and that difference is what the original
            // dithers. In eighths of a tile, as UWCornerLevel takes it.
            float UWNewtonLength(float3 pOView)
            {
                float3 lOAxis = floor(pOView * 4.0 / 32.0);
                float lfSquare = dot(lOAxis, lOAxis);
                float lfRoot = 64.0;

                for (int liStep = 0; liStep < 4; liStep++)
                    lfRoot = floor((lfRoot + floor(lfSquare / max(lfRoot, 1.0))) * 0.5);

                return lfRoot;
            }

            // The corner's distance in the ORIGINAL'S VIEW SPACE, as smooth_over takes it: scale_matrix has
            // folded the view into the matrix, X times 0.7887, Y as it is, depth times the zoom 0.7629
            // (UWClassicLayout.Frame), so that the frustum's planes stand at |x| = z and |y| = z (code_pnt).
            // A corner outside is pulled to those planes, about where the polygon's clipped corner lies:
            // the top of a wall is the edge of the screen, not the ceiling. Measured 2026-10-30 on a wall 1.68
            // tiles ahead: 4 at the foot, 6 at the clipped top, the rows of LIGHT.DAT read from the picture
            // 4 to 5. In world units this came out two levels too dark.
            float UWDungeonCornerDistance(float3 pOPositionWS)
            {
                float3 lOView = TransformWorldToView(pOPositionWS);
                float lfDepth = max(-lOView.z * _UWViewScale.y, 0.0);
                float2 lOSide = float2(lOView.x * _UWViewScale.x, lOView.y);

                return UWNewtonLength(float3(clamp(lOSide, -lfDepth, lfDepth), lfDepth));
            }

            Varyings UWVertex(Attributes IN)
            {
                Varyings OUT = (Varyings)0;

                VertexPositionInputs positions = GetVertexPositionInputs(IN.positionOS.xyz);

                OUT.positionCS = positions.positionCS;
                OUT.positionWS = positions.positionWS;
                OUT.uv = IN.uv;
                OUT.cornerLevel = UWCornerLevel(UWDungeonCornerDistance(positions.positionWS), positions.positionWS,
                    _WorldSpaceCameraPos);

                return OUT;
            }

            // The scrambled texture mapper of the hallucination: UWScrambleUV, UWHallucination.hlsl.

            half4 UWFragment(Varyings IN) : SV_Target
            {
                // The face's normal first: the depth effect shifts the coordinate before the index
                // is read (UWPaletteEffects.hlsl, UWPaletteParallaxUv).
                float3 lNormal = UWFacingNormal(IN.positionWS);
                float2 lUv = UWScrambleUV(float3(UWPaletteParallaxUv(IN.uv.xy, IN.uv.z, IN.positionWS, lNormal), IN.uv.z));
                half4 raw = SAMPLE_TEXTURE2D_ARRAY(_IndexArray, sampler_IndexArray, lUv, IN.uv.z);

                // The part of the hallucination's light table (UWHallucination.hlsl).
                UWSurfacePart = UWDungeonSurfacePart(IN.uv.z, IN.positionWS.y);

                // The palette renderer's own effects in shade levels (UWPaletteEffects.hlsl).
                float lfGrime = _UWPaletteGrime > 0.0 ? UWGrimeAmount(IN.positionWS, lNormal) : 0.0;

                UWLightCap = min(UWPaletteLightCap(IN.positionWS, lNormal), UWLavaGlowCap(raw, IN.positionWS));
                UWGrimeTintMix = lfGrime;

                half4 colour = UWPaletteColourAtLevel(raw, IN.cornerLevel,
                    UWPaletteEffectLevel(IN.positionCS) + (_UWPaletteGrime * lfGrime)
                        + UWGroundShadowLevel(IN.positionWS, lNormal)
                        + UWReliefLevel(IN.positionWS, lNormal, lUv, IN.uv.z)
                        + UWPaletteSelfShadowLevel(lUv, IN.uv.z, IN.positionWS, lNormal),
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
