// Stylized lit shader for the hero models (Q'uq'umatz priest and Mage): the color comes from the mesh vertex colors
// (the `Col` attribute of the Blender models, exported as sRGB, with the light, gradients and occlusion painted in),
// there are no textures. Custom lighting: a soft toon ramp on a half lambert for the main light (with its shadows),
// a cool shadow tint, ambient from the light probes (SH) plus a sky/ground gradient, and a warm Fresnel rim, so the
// hero reads clearly from the oblique top-down camera.
// BodyFlash drives the hit flash through a MaterialPropertyBlock: _Tint multiplies the color, _EmissionColor is added.
// Works with skinned meshes, GPU instancing and the SRP Batcher.
Shader "WSF/Hero Vertex Color"
{
    Properties
    {
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        [Toggle] _SrgbVertexColor ("Vertex Color Is sRGB", Float) = 1
        [HDR] _EmissionColor ("Emission (hit flash)", Color) = (0, 0, 0, 0)

        [Header(Main Light)]
        _RampCenter ("Ramp Center (half lambert)", Range(0, 1)) = 0.45
        _RampSoftness ("Ramp Softness", Range(0.01, 0.5)) = 0.2
        _ShadowTint ("Shadow Tint (light on the dark side)", Color) = (0.42, 0.47, 0.62, 1)

        [Header(Ambient)]
        _AmbientStrength ("Ambient From Light Probes", Range(0, 2)) = 0.8
        _SkyColor ("Sky Color", Color) = (0.62, 0.68, 0.78, 1)
        _GroundColor ("Ground Color", Color) = (0.3, 0.26, 0.2, 1)
        _HemisphereStrength ("Sky And Ground Strength", Range(0, 1)) = 0.25

        [Header(Rim)]
        _RimColor ("Rim Color", Color) = (1, 0.76, 0.45, 1)
        _RimStrength ("Rim Strength", Range(0, 2)) = 0.55
        _RimPower ("Rim Power (higher = thinner)", Range(0.5, 8)) = 3
        _RimShadowSide ("Rim On The Dark Side", Range(0, 1)) = 0.35

        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _Tint;
            half _SrgbVertexColor;
            half4 _EmissionColor;
            half _RampCenter;
            half _RampSoftness;
            half4 _ShadowTint;
            half _AmbientStrength;
            half4 _SkyColor;
            half4 _GroundColor;
            half _HemisphereStrength;
            half4 _RimColor;
            half _RimStrength;
            half _RimPower;
            half _RimShadowSide;
            half _Cull;
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
                output.fogFactor = ComputeFogFactor(positions.positionCS.z);
                return output;
            }

            float4 GetShadowCoord(float3 positionWS)
            {
            #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                return ComputeScreenPos(TransformWorldToHClip(positionWS));
            #else
                return TransformWorldToShadowCoord(positionWS);
            #endif
            }

            half4 Fragment(Varyings input, FRONT_FACE_TYPE isFrontFace : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half3 vertexColor = input.color.rgb;
                half3 albedo = _SrgbVertexColor > 0.5h ? SRGBToLinear(vertexColor) : vertexColor;
                albedo *= _Tint.rgb;

                // Feathers and the cape are flat parts: light the back side too when culling is off.
                float3 normal = normalize(input.normalWS) * (IS_FRONT_VFACE(isFrontFace, 1.0, -1.0));
                float3 viewDirection = GetWorldSpaceNormalizeViewDir(input.positionWS);
                Light mainLight = GetMainLight(GetShadowCoord(input.positionWS));

                // Soft toon ramp on a half lambert; the dark side gets a cool part of the light instead of black.
                half halfLambert = dot(normal, mainLight.direction) * 0.5h + 0.5h;
                half ramp = smoothstep(_RampCenter - _RampSoftness, _RampCenter + _RampSoftness, halfLambert);
                half lightAmount = ramp * mainLight.shadowAttenuation * mainLight.distanceAttenuation;
                half3 direct = mainLight.color * lerp(_ShadowTint.rgb, half3(1.0h, 1.0h, 1.0h), lightAmount);

                half3 hemisphere = lerp(_GroundColor.rgb, _SkyColor.rgb, normal.y * 0.5h + 0.5h);
                half3 ambient = SampleSH(normal) * _AmbientStrength + hemisphere * _HemisphereStrength;

                // Warm rim of light on the silhouette, weaker on the side turned away from the main light.
                half fresnel = pow(1.0h - saturate(dot(normal, viewDirection)), _RimPower);
                half rimMask = lerp(_RimShadowSide, 1.0h, lightAmount);
                half3 rim = _RimColor.rgb * (fresnel * rimMask * _RimStrength);

                half3 color = albedo * (direct + ambient) + rim * lerp(half3(1.0h, 1.0h, 1.0h), _Tint.rgb, 0.5h);
                color += _EmissionColor.rgb;
                color = MixFog(color, input.fogFactor);
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

        // Depth and normals for the screen space effects (SSAO) when the renderer asks for them.
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

            struct DepthNormalsAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct DepthNormalsVaryings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
            };

            DepthNormalsVaryings DepthNormalsVertex(DepthNormalsAttributes input)
            {
                DepthNormalsVaryings output = (DepthNormalsVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 DepthNormalsFragment(DepthNormalsVaryings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
            #if defined(_GBUFFER_NORMALS_OCT)
                float2 octahedral = PackNormalOctQuadEncode(normalWS) * 0.5 + 0.5;
                return half4(PackFloat2To888(octahedral), 0.0);
            #else
                return half4(normalWS, 0.0);
            #endif
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
