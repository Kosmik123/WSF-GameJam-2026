// Lit shader for the biome assets and terrain: the color comes from the mesh vertex colors (the `Col`
// attribute of the Blender assets, exported as sRGB), there are no textures.
// Supports GPU instancing (biome assets are drawn with Graphics.RenderMeshInstanced) and the SRP Batcher.
Shader "WSF/Vertex Color Lit"
{
    Properties
    {
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        [Toggle] _SrgbVertexColor ("Vertex Color Is sRGB", Float) = 1
        _Emission ("Emission (glowing assets)", Range(0, 8)) = 0
        _WarmGlow ("Glow Of Warm Colors (lava on terrain)", Range(0, 8)) = 0
        [Toggle] _Faceted ("Faceted (one color and flat lighting per triangle, for smooth meshes like the terrain)", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull (Back = one-sided, Off = two-sided, e.g. for leaves)", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _Tint;
            half _SrgbVertexColor;
            half _Emission;
            half _WarmGlow;
            half _Cull;
            half _Faceted;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                half4 color : COLOR;
                half fogFactor : TEXCOORD2;
                nointerpolation half4 flatColor : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.color = input.color;
                output.flatColor = input.color;
                output.fogFactor = ComputeFogFactor(positions.positionCS.z);
                return output;
            }

            half4 Fragment(Varyings input, FRONT_FACE_TYPE isFrontFace : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                bool isFaceted = _Faceted > 0.5h;
                half3 vertexColor = isFaceted ? input.flatColor.rgb : input.color.rgb;
                half3 albedo = _SrgbVertexColor > 0.5h ? SRGBToLinear(vertexColor) : vertexColor;
                albedo *= _Tint.rgb;

                // Leaves, petals and lilies are single planes drawn from both sides: light the back side too.
                float3 normal = normalize(input.normalWS) * (IS_FRONT_VFACE(isFrontFace, 1.0, -1.0));
                if (isFaceted)
                {
                    // Flat normal of the triangle, turned to the side of the smooth one.
                    float3 flatNormal = normalize(cross(ddy(input.positionWS), ddx(input.positionWS)));
                    normal = dot(flatNormal, normal) < 0.0 ? -flatNormal : flatNormal;
                }
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half lambert = saturate(dot(normal, mainLight.direction));
                half3 lit = albedo * mainLight.color * (lambert * mainLight.shadowAttenuation * mainLight.distanceAttenuation);
                half3 ambient = albedo * SampleSH(normal);

                // Lava on a single-material terrain: strongly warm colors (red much brighter than blue) glow.
                half warm = saturate((vertexColor.r - vertexColor.b - 0.55h) * 5.0h);
                half3 emission = albedo * (_Emission + warm * _WarmGlow);

                half3 color = MixFog(lit + ambient + emission, input.fogFactor);
                return half4(color, 1.0h);
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
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex ShadowVertex
            #pragma fragment ShadowFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 ShadowVertex(ShadowAttributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirection = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirection = _LightDirection;
            #endif

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirection));
            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                return positionCS;
            }

            half4 ShadowFragment() : SV_Target
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
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex DepthVertex
            #pragma fragment DepthFragment
            #pragma multi_compile_instancing

            struct DepthAttributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 DepthVertex(DepthAttributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return TransformObjectToHClip(input.positionOS.xyz);
            }

            half DepthFragment() : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
