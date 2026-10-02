// THE HALLUCINATION'S PALETTE ON THE WHOLE SCREEN (UWHallucinationState.PaletteEffect, per user
// 2026-09-27): the original swaps the VGA palette, so every pixel changes - panels, paperdoll,
// flasks and scroll included. Our interface pictures are colours, not indices, but each of
// them is a colour of palette 0; this shader turns it back into its index through the cube of
// the palette renderer (_UWInverseLookup, see UWTransparencyTables.BuildInverseLookup) and
// paints that index in the current palette (row 0 of _UWColourTable, which holds the
// hallucination's palette meanwhile). Put on every interface Graphic by UWUiPaletteSwap only
// while the effect runs; otherwise the default UI material draws.
//
// Built like UI/Default: the same stencil block for masks, clipping and alpha clip, the font
// atlas added through _TextureSampleAdd.
Shader "UW/UIPaletteSwap"
{
    Properties
    {
        [PerRendererData] _MainTex ("Picture", 2D) = "white" {}
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
            Name "UWPaletteSwap"

            CGPROGRAM
            #pragma vertex UWVertex
            #pragma fragment UWFragment
            #pragma target 3.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;

            sampler2D _UWColourTable;
            sampler3D _UWInverseLookup;

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

            fixed4 UWFragment(Varyings IN) : SV_Target
            {
                fixed4 colour = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;

                // The colour back to its index (six bits a channel), then the index in the
                // current palette.
                float3 cell = floor(saturate(colour.rgb) * 63.0 + 0.5);
                float index = floor(tex3D(_UWInverseLookup, (cell + 0.5) / 64.0).r * 255.0 + 0.5);

                colour.rgb = tex2D(_UWColourTable, float2((index + 0.5) / 256.0, 0.5 / 16.0)).rgb;

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
