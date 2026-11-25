// THE WEAPON IN THE HAND BY THE TEXTURES' LOOK (per user 2026-11-25, UWTextureLook): the picture blended between its
// texels as the sprites in the world are (UWSpriteFilter.hlsl), from the same global _UWSpriteFilter - 0 the texel as
// the texture's own filter reads it (point: exactly the default UI's picture), 1 smooth (bilinear, as in 2012). The outline
// stays the nearest texel's opacity, the colour is the four texels mixed by their opacity, so a clear neighbour leaves no
// fringe. The four texels are clamped to the picture, whatever the texture's wrap mode.
//
// Structure as UW/PixelArtUI's and Unity's UI default, so that masking, stencil and the CanvasGroup's alpha keep working -
// see UWWeaponFilterUI in UWPixelArtUI.cs.
Shader "UW/WeaponUI"
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
            Name "UWWeapon"

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
            float4 _ClipRect;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;

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

            float _UWSpriteFilter;

            float4 UWPremultiplied(float2 uv)
            {
                float4 texel = tex2Dlod(_MainTex, float4(uv, 0, 0));

                return float4(texel.rgb * texel.a, texel.a);
            }

            fixed4 UWFragment(Varyings IN) : SV_Target
            {
                float2 size = _MainTex_TexelSize.zw;
                fixed4 colour = tex2D(_MainTex, IN.texcoord);

                if (_UWSpriteFilter > 0.5 && colour.a > 0.0)
                {
                    float2 at = IN.texcoord * size - 0.5;
                    float2 base = floor(at);
                    float2 blend = at - base;

                    float2 low = clamp(base + 0.5, 0.5, size - 0.5) / size;
                    float2 high = clamp(base + 1.5, 0.5, size - 0.5) / size;

                    float4 p0 = lerp(UWPremultiplied(low), UWPremultiplied(float2(high.x, low.y)), blend.x);
                    float4 p1 = lerp(UWPremultiplied(float2(low.x, high.y)), UWPremultiplied(high), blend.x);
                    float4 mixed = lerp(p0, p1, blend.y);

                    colour.rgb = mixed.a > 0.0001 ? mixed.rgb / mixed.a : colour.rgb;
                }

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
