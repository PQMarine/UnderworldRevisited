// The modern UI's pictures in colour - the leather, the slot circles, the shelf, the flasks,
// the paperdoll - at ANY scale (UWModernHud.PixelScale, free since 2026-10-03, per user): each
// texel stays a hard square and only the one screen pixel across a seam between two texels is
// blended. No uneven pixel widths as with plain point filtering at a scale like 2.5, and no blur
// as with bilinear. At a whole-number scale it gives exactly the texels.
//
// The four texels around the seam are fetched at their centres and mixed with their opacity,
// so the texture's own filter mode does not matter - the classic HUD's point-filtered pictures
// can be drawn with it unchanged.
//
// Structure as in Unity's UI default, so that masking, stencil and the CanvasGroup's alpha keep
// working - see UWPixelArtUI.cs.
Shader "UW/PixelArtUI"
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
            Name "UWPixelArt"

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

            float4 UWPremultiplied(float2 uv)
            {
                float4 texel = tex2D(_MainTex, uv);

                return float4(texel.rgb * texel.a, texel.a);
            }

            fixed4 UWFragment(Varyings IN) : SV_Target
            {
                float2 size = _MainTex_TexelSize.zw;
                float2 at = IN.texcoord * size;
                float2 seam = floor(at + 0.5);
                float2 blend = clamp((at - seam) / max(fwidth(at), 1e-5), -0.5, 0.5) + 0.5;
                float2 low = (seam - 0.5) / size;
                float2 high = (seam + 0.5) / size;

                float4 p0 = lerp(UWPremultiplied(low), UWPremultiplied(float2(high.x, low.y)), blend.x);
                float4 p1 = lerp(UWPremultiplied(float2(low.x, high.y)), UWPremultiplied(high), blend.x);
                float4 mixed = lerp(p0, p1, blend.y);

                fixed4 colour = fixed4(mixed.a > 0.0001 ? mixed.rgb / mixed.a : mixed.rgb, mixed.a);

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
