// Semi-transparent water surface (URP). The deeper the water under a pixel (from the camera depth texture), the darker and more opaque it is.
// Light foam where the water meets the shore, moving ripples and a sun highlight. Without a depth texture the water is treated as deep.
Shader "WSF/Water"
{
    Properties
    {
        _ShallowColor ("Shallow Color", Color) = (0.32, 0.55, 0.45, 0.35)
        _DeepColor ("Deep Color", Color) = (0.05, 0.16, 0.18, 0.85)
        _DepthRange ("Depth Range (m)", Float) = 3
        _FoamColor ("Foam Color", Color) = (0.85, 0.9, 0.8, 0.8)
        _FoamWidth ("Foam Width (m)", Float) = 0.5
        _RippleScale ("Ripple Scale", Float) = 0.35
        _RippleSpeed ("Ripple Speed", Float) = 0.6
        _RippleStrength ("Ripple Strength", Range(0, 1)) = 0.35
        _Glossiness ("Glossiness", Range(1, 256)) = 90
        _SpecularStrength ("Specular Strength", Range(0, 2)) = 0.7
        _FresnelStrength ("Fresnel Strength", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent-10"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "Water"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                float _DepthRange;
                half4 _FoamColor;
                float _FoamWidth;
                float _RippleScale;
                float _RippleSpeed;
                half _RippleStrength;
                half _Glossiness;
                half _SpecularStrength;
                half _FresnelStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 screenPosition : TEXCOORD2;
                float fogFactor : TEXCOORD3;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positions =GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.screenPosition = ComputeScreenPos(positions.positionCS);
                output.fogFactor = ComputeFogFactor(positions.positionCS.z);
                return output;
            }

            // Two crossing wave trains in the plane of the surface: a cheap animated normal.
            float3 GetRippleNormal(float3 positionWS, float3 normalWS)
            {
                float3 tangent = normalize(cross(normalWS, abs(normalWS.y) < 0.99 ? float3(0, 1, 0) : float3(1, 0, 0)));
                float3 bitangent = cross(normalWS, tangent);
                float2 p = float2(dot(positionWS, tangent), dot(positionWS, bitangent)) * _RippleScale;
                float time = _Time.y * _RippleSpeed;

                float2 slope = float2(cos(p.x * 1.0 + time * 1.3) + 0.6 * cos(p.x * 2.3 - p.y * 1.7 + time * 2.1),
                                      cos(p.y * 1.1 - time * 1.1) + 0.6 * cos(p.y * 2.9 + p.x * 1.3 - time * 1.7));
                slope *= _RippleStrength * 0.35;
                return normalize(normalWS - tangent * slope.x - bitangent * slope.y);
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                float2 screenUV = input.screenPosition.xy / input.screenPosition.w;
                float sceneDepth = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float surfaceDepth = LinearEyeDepth(input.positionCS.z, _ZBufferParams);
                float waterDepth = max(0.0, sceneDepth - surfaceDepth);

                half depth01 = saturate(waterDepth / max(_DepthRange, 0.01));
                half4 color = lerp(_ShallowColor, _DeepColor, depth01);

                float3 normalWS = GetRippleNormal(input.positionWS, normalize(input.normalWS));
                float3 viewDirection = normalize(GetWorldSpaceViewDir(input.positionWS));
                Light light = GetMainLight(TransformWorldToShadowCoord(input.positionWS));

                half diffuse = saturate(dot(normalWS, light.direction)) * 0.5 + 0.5;
                color.rgb *= light.color * diffuse * lerp(0.6, 1.0, light.shadowAttenuation) + SampleSH(normalWS);

                half fresnel = pow(1.0 - saturate(dot(normalWS, viewDirection)), 4.0) * _FresnelStrength;
                half3 sky = SampleSH(normalWS) * 1.5 + half3(0.05, 0.1, 0.15);
                color.rgb = lerp(color.rgb, sky, fresnel * 0.6);
                color.a = saturate(color.a + fresnel * 0.4);

                float3 halfVector = normalize(light.direction + viewDirection);
                half specular = pow(saturate(dot(normalWS, halfVector)), _Glossiness) * _SpecularStrength * light.shadowAttenuation;
                color.rgb += light.color * specular;
                color.a = saturate(color.a + specular);

                half foam = 1.0 - saturate(waterDepth / max(_FoamWidth, 0.01));
                foam *= 0.75 + 0.25 * sin(_Time.y * 2.0 + dot(input.positionWS, float3(0.7, 0.3, 0.5)) * 3.0);
                color.rgb = lerp(color.rgb, _FoamColor.rgb, foam * _FoamColor.a);
                color.a = max(color.a, foam * _FoamColor.a);

                color.rgb = MixFog(color.rgb, input.fogFactor);
                return color;
            }
            ENDHLSL
        }
    }
}
