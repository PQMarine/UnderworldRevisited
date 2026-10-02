// The flat-coloured faces of the baked-in 3D models in the palette renderer - chests,
// barrels, shrine, bridge and anything else with faces that have no texture of their own.
//
// SEPARATE SHADER ON PURPOSE. These faces do not take their palette index from a
// texture but from the second UV set at the vertex (x the index, y the additional
// darkening plus one - see UWObjectSpawner). A shader that reads a vertex channel
// thereby places a requirement on EVERY mesh drawn with it, and
// that requirement is written down nowhere in the code.
//
// So at first the branch was tried in the wall shader and the sprite shader.
// That went wrong three times: walls, door frames and finally animated sprites turned black,
// because their meshes do not carry the channel and Unity does not guarantee zeros for a
// missing channel. For the animated sprites it could not even be fixed, because their mesh
// changes every frame. With a separate shader the requirement sits where the data is
// (2026-09-05).
//
// The distance here counts per PIXEL and not to the pivot as with a sprite: a
// chest face is part of a solid model and should shade across its depth.
Shader "UW/ModelPalette"
{
    Properties
    {
        // Which side is culled: 0 none, 1 the front, 2 the back.
        // See UW/Decal - the portcullis material uses 2 (CullMode.Back, see UWLevelLoader).
        [HideInInspector] _Cull ("Culling", Float) = 0

        // The floor of the model's own tile, per renderer (UWObjectSpawner) - its origin may lie
        // below it, as the shrine's does. Only read with the keyword _UW_OWN_TILE, which only the
        // model face material carries - see UWOwnTile.hlsl.
        [HideInInspector] _UWModelFloorY ("Model Floor", Float) = -100000
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "AlphaTest"
        }
        LOD 100

        Pass
        {
            Name "UWUnlit"
            Tags { "LightMode" = "UniversalForward" }

            Cull [_Cull]
            ZWrite On

            HLSLPROGRAM
            #pragma vertex UWVertex
            #pragma fragment UWFragment
            #pragma target 3.0
            #pragma multi_compile_local_fragment _ _UW_OWN_TILE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "UWPaletteLookup.hlsl"
            #include "UWOwnTile.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Cull;
                float _UWModelFloorY;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS  : POSITION;

                // x the palette index, y the vertex's additional darkening plus
                // one. The darkening varies across the face and gives it its contour;
                // the index is the same everywhere within a face, so its interpolation
                // does no harm.
                float2 paletteData : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float2 paletteData : TEXCOORD1;
            };

            Varyings UWVertex(Attributes IN)
            {
                Varyings OUT = (Varyings)0;

                VertexPositionInputs positions = GetVertexPositionInputs(IN.positionOS.xyz);

                OUT.positionCS = positions.positionCS;
                OUT.positionWS = positions.positionWS;
                OUT.paletteData = IN.paletteData;

                return OUT;
            }

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

                half4 colour = UWPaletteColourIndexed(IN.paletteData.x,
                    max(IN.paletteData.y - 1.0, 0.0),
                    IN.positionWS, _WorldSpaceCameraPos, IN.positionCS.xy);

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
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }
    }

    FallBack Off
}
