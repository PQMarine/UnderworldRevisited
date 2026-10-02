// UI icons via the palette: backpack slots, hand slots, spell icons,
// runes, containers, drag icon.
//
// The image does not hold a finished icon, but the palette index in the red channel and the
// opacity in the alpha channel (see UWIconTextureBuilder). The colour is only produced here via
// the same lookup textures as the world (see UWShadePalette), just without distance shading:
// the original draws the UI unshaded, so always brightness level 0.
//
// THIS REMOVES THE PRE-BAKING. Previously UWCycledIcon pre-baked, for every flickering icon, all
// steps of the palette rotation as finished images and switched through them at runtime.
// That only ran in two places - spell icons and hand slots -; in the backpack, in containers
// and on the drag icon nothing flickered. Through the shader every icon rotates along, without a special path.
//
// Structure as in Unity's UI default, so that masking, stencil and depth test of the
// UI keep working unchanged - only the colour lookup is swapped.
Shader "UW/IconPalette"
{
    Properties
    {
        [PerRendererData] _MainTex ("Icon (palette indices)", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "UWIcon"

            CGPROGRAM
            #pragma vertex UWVertex
            #pragma fragment UWFragment
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            sampler2D _MainTex;
            fixed4 _Color;
            float4 _ClipRect;
            float4 _MainTex_ST;

            // The same lookup textures as the world, set by
            // UWShadePalette.ApplyGlobals. Row 0 of the colour table is brightness level 0,
            // i.e. the original colours - the UI is not darkened.
            sampler2D _UWColourTable;
            sampler2D _UWRotationTable;
            float _UWRotationStep;
            float _UWRotationStepCount;

            struct Attributes
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings UWVertex(Attributes IN)
            {
                Varyings OUT;

                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                OUT.worldPosition = IN.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = TRANSFORM_TEX(IN.texcoord, _MainTex);
                OUT.color = IN.color * _Color;

                return OUT;
            }

            // The pixel centre of a column or row, so that point filtering
            // does not fall between two entries.
            float2 UWTableUV(float pfColumn, float pfRow, float pfWidth, float pfHeight)
            {
                return float2((pfColumn + 0.5) / pfWidth, (pfRow + 0.5) / pfHeight);
            }

            fixed4 UWFragment(Varyings IN) : SV_Target
            {
                fixed4 raw = tex2D(_MainTex, IN.texcoord);

                // Recover the integer 0..255 from the channel value.
                float index = floor((raw.r * 255.0) + 0.5);

                // The palette rotation animates flames and water - the same number as in the
                // world, set per frame.
                float steps = max(_UWRotationStepCount, 1.0);
                float rotated = floor((tex2D(_UWRotationTable,
                    UWTableUV(index, _UWRotationStep, 256.0, steps)).r * 255.0) + 0.5);

                // Row 0: brightness level 0, no darkening.
                fixed4 colour = tex2D(_UWColourTable, UWTableUV(rotated, 0.0, 256.0, 16.0));

                colour.a = raw.a;
                colour *= IN.color;

                #ifdef UNITY_UI_CLIP_RECT
                colour.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(colour.a - 0.001);
                #endif

                return colour;
            }
            ENDCG
        }
    }
}
