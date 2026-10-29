// UI version of MindTheGap/ColorMorph for Images on a Canvas.
// Unlit, respects the Image color/alpha, and supports UI masking.
// The pattern uses canvas-space position (in UI pixels), so it does
// not stretch when the Image is resized - a growing bar reveals more
// of the same pattern.
Shader "MindTheGap/ColorMorphUI"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)

        [Header(Colors)]
        _ColorA ("Color A", Color) = (0.0, 0.9, 1.0, 1.0)
        _ColorB ("Color B", Color) = (1.0, 0.1, 0.8, 1.0)

        [Header(Pattern)]
        _Scale ("Pattern Scale (per UI pixel)", Float) = 0.03
        _Speed ("Flow Speed", Float) = 0.5
        _Warp ("Morph Amount", Range(0, 4)) = 1.5
        _Softness ("Gradient Softness", Range(0.01, 1)) = 0.6

        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
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
            Name "Default"

            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

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
                float4 canvasPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;

            fixed4 _ColorA;
            fixed4 _ColorB;
            float _Scale;
            float _Speed;
            float _Warp;
            float _Softness;

            Varyings Vert(Attributes input)
            {
                Varyings output;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.canvasPosition = input.vertex;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.texcoord = TRANSFORM_TEX(input.texcoord, _MainTex);
                output.color = input.color * _Color;

                return output;
            }

            // Same pattern as ColorMorph: 0 for Color A, 1 for Color B.
            float MorphMask(float2 position)
            {
                float2 p = position * _Scale;
                float t = _Time.y * _Speed;

                float warp = sin(p.y * 1.3 + t) + sin(p.x * 0.6 - t * 1.1);

                float value = sin(p.x + warp * _Warp + t)
                            + sin(p.x * 0.7 + p.y + warp * _Warp * 0.5 - t * 0.8);

                float gradient = value * 0.25 + 0.5;

                float halfWidth = _Softness * 0.5;
                return smoothstep(0.5 - halfWidth, 0.5 + halfWidth, gradient);
            }

            fixed4 Frag(Varyings input) : SV_Target
            {
                half4 sprite = tex2D(_MainTex, input.texcoord) + _TextureSampleAdd;

                fixed3 morphColor = lerp(_ColorA.rgb, _ColorB.rgb, MorphMask(input.canvasPosition.xy));

                half4 color = half4(morphColor, 1.0) * sprite * input.color;

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(input.canvasPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                return color;
            }
            ENDCG
        }
    }
}
