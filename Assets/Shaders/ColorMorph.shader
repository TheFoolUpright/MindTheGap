// Two-color gradient that flows and morphs across an object's surface.
// The pattern uses object-space position, so it stays attached to the
// object while it is carried or rotated.
Shader "MindTheGap/ColorMorph"
{
    Properties
    {
        [Header(Colors)]
        _ColorA ("Color A", Color) = (0.0, 0.9, 1.0, 1.0)
        _ColorB ("Color B", Color) = (1.0, 0.1, 0.8, 1.0)

        [Header(Pattern)]
        _Scale ("Pattern Scale", Float) = 3.0
        _Speed ("Flow Speed", Float) = 0.5
        _Warp ("Morph Amount", Range(0, 4)) = 1.5
        _Softness ("Gradient Softness", Range(0.01, 1)) = 0.6

        [Header(Surface)]
        _Smoothness ("Smoothness", Range(0, 1)) = 0.4
        _EmissionStrength ("Emission Strength", Range(0, 2)) = 0.3
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        // Material properties live in this buffer for SRP Batcher support.
        CBUFFER_START(UnityPerMaterial)
            half4 _ColorA;
            half4 _ColorB;
            float _Scale;
            float _Speed;
            float _Warp;
            float _Softness;
            half _Smoothness;
            half _EmissionStrength;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);

                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.positionOS = input.positionOS.xyz;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);

                return output;
            }

            // Returns 0 for Color A, 1 for Color B, with a soft gradient between.
            float MorphMask(float3 positionOS)
            {
                float3 p = positionOS * _Scale;
                float t = _Time.y * _Speed;

                // The first wave bends the second, so the color boundary
                // wobbles and changes shape instead of sliding in straight bands.
                float warp = sin(p.y * 1.3 + t) + sin(p.z * 1.7 - t * 1.1);

                float value = sin(p.x + warp * _Warp + t)
                            + sin(p.z + p.y * 0.7 + warp * _Warp * 0.5 - t * 0.8);

                // value is in [-2, 2]; remap to [0, 1].
                float gradient = value * 0.25 + 0.5;

                float halfWidth = _Softness * 0.5;
                return smoothstep(0.5 - halfWidth, 0.5 + halfWidth, gradient);
            }

            half3 LightContribution(Light light, half3 albedo, half3 normalWS, half3 viewDirWS)
            {
                half attenuation = light.distanceAttenuation * light.shadowAttenuation;
                half3 radiance = light.color * attenuation;

                half diffuse = saturate(dot(normalWS, light.direction));

                half3 halfDir = SafeNormalize(light.direction + viewDirWS);
                half specPower = exp2(10.0 * _Smoothness + 1.0);
                half specular = pow(saturate(dot(normalWS, halfDir)), specPower) * _Smoothness;

                return radiance * (albedo * diffuse + specular);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half mask = MorphMask(input.positionOS);
                half3 albedo = lerp(_ColorA.rgb, _ColorB.rgb, mask);

                half3 normalWS = normalize(input.normalWS);
                half3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                half3 color = SampleSH(normalWS) * albedo;
                color += LightContribution(mainLight, albedo, normalWS, viewDirWS);

                #if defined(_ADDITIONAL_LIGHTS)
                uint lightCount = GetAdditionalLightsCount();
                for (uint i = 0; i < lightCount; i++)
                {
                    Light light = GetAdditionalLight(i, input.positionWS, half4(1, 1, 1, 1));
                    color += LightContribution(light, albedo, normalWS, viewDirWS);
                }
                #endif

                // Self-glow keeps the colors readable in dark areas.
                color += albedo * _EmissionStrength;

                color = MixFog(color, input.fogFactor);

                return half4(color, 1.0);
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

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag

            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings ShadowVert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                float3 lightDirectionWS = _LightDirection;
                #endif

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                output.positionCS = ApplyShadowClamping(positionCS);

                return output;
            }

            half4 ShadowFrag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings DepthVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half DepthFrag(Varyings input) : SV_Target
            {
                return input.positionCS.z;
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
