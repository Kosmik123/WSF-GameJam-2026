// Soft, lumpy dust puff for particles (URP). No texture: the round shape with a ragged edge is computed from the UV.
// Color = particle (vertex) color * tint, lit a little by the main light so the dust is not glowing in the shade.
Shader "WSF/Particle Dust"
{
    Properties
    {
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _Softness ("Edge Softness", Range(0.05, 1)) = 0.75
        _Lumpiness ("Lumpiness", Range(0, 1)) = 0.6
        _LightInfluence ("Light Influence", Range(0, 1)) = 0.6
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half _Softness;
                half _Lumpiness;
                half _LightInfluence;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                float fogFactor : TEXCOORD1;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                float2 p = input.uv * 2.0 - 1.0;
                float radius = length(p);
                float angle = atan2(p.y, p.x);

                // Ragged edge (particles get a random rotation, so the lumps differ) and a few holes inside.
                float edge = sin(angle * 3.0 + 1.3) * 0.5 + sin(angle * 7.0 + 4.1) * 0.3 + sin(angle * 11.0) * 0.2;
                float inner = sin(p.x * 7.3 + 2.0) * sin(p.y * 6.1 + 0.7);
                float shape = radius + edge * 0.18 * _Lumpiness;
                half alpha = 1.0h - smoothstep(1.0 - _Softness, 1.0, shape);
                alpha *= 1.0h - _Lumpiness * 0.35h * saturate(inner * 0.5 + 0.5) * (1.0 - radius);

                half4 color = input.color * _Tint;
                half3 light = GetMainLight().color;
                half brightness = saturate(max(light.r, max(light.g, light.b)));
                color.rgb *= lerp(1.0h, 0.35h + 0.65h * brightness, _LightInfluence);
                color.a *= saturate(alpha);
                color.rgb = MixFog(color.rgb, input.fogFactor);
                return color;
            }
            ENDHLSL
        }
    }
}
