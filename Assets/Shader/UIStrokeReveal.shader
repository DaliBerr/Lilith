Shader "UI/StrokeRevealImage"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _Progress ("Progress", Range(0,1)) = 0
        _Softness ("Softness", Range(0.0001,0.2)) = 0.02
        _RevealDir ("Reveal Direction", Vector) = (1,0,0,0)

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255

        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
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
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex        : SV_POSITION;
                fixed4 color         : COLOR;
                float2 uv            : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float4 _MainTex_ST;

            float _Progress;
            float _Softness;
            float4 _RevealDir;

            float4 _ClipRect;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv) * i.color;

                float2 dir = _RevealDir.xy;
                float lenSq = dot(dir, dir);
                if (lenSq < 0.00001)
                {
                    dir = float2(1, 0);
                }
                else
                {
                    dir = normalize(dir);
                }

                float p00 = dot(float2(0, 0), dir);
                float p10 = dot(float2(1, 0), dir);
                float p01 = dot(float2(0, 1), dir);
                float p11 = dot(float2(1, 1), dir);

                float minP = min(min(p00, p10), min(p01, p11));
                float maxP = max(max(p00, p10), max(p01, p11));

                float2 uv01 = i.uv;
                float p = dot(uv01, dir);
                float t = saturate((p - minP) / max(0.00001, maxP - minP));

                float mask;
                if (_Progress <= 0.0001)
                {
                    mask = 0.0;
                }
                else if (_Progress >= 0.9999)
                {
                    mask = 1.0;
                }
                else
                {
                    mask = 1.0 - smoothstep(_Progress - _Softness, _Progress + _Softness, t);
                }

                c.a *= mask;

                #ifdef UNITY_UI_CLIP_RECT
                c.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(c.a - 0.001);
                #endif

                return c;
            }
            ENDCG
        }
    }
}