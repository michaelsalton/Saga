Shader "Saga/Glass"
{
    Properties
    {
        [Header(Color)]
        [MainColor] _BaseColor ("Base Color", Color) = (1, 1, 1, 1)

        [Header(Surface)]
        [Normal] _NormalMap ("Normal Map", 2D) = "bump" {}
        _NormalScale ("Normal Scale", Range(0, 2)) = 1

        [Header(Gloss)]
        _Smoothness ("Smoothness", Range(0, 1)) = 0.9
        _Reflectivity ("Reflectivity", Range(0, 1)) = 0.05
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
            "Queue"="Geometry"
            "RenderPipeline"="UniversalPipeline"
        }

        Pass
        {
            Name "GlassForward"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _NormalMap_ST;
                float _NormalScale;
                float _Smoothness;
                float _Reflectivity;
            CBUFFER_END

            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : tangent;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 tangentWS : TEXCOORD2;
                float3 bitangentWS : TEXCOORD3;
                float3 positionWS : TEXCOORD4;
            };

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;

                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionWS = positionWS;
                OUT.positionHCS = TransformWorldToHClip(positionWS);
                OUT.uv = TRANSFORM_TEX(IN.uv, _NormalMap);

                VertexNormalInputs normalInputs = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);
                OUT.normalWS = normalInputs.normalWS;
                OUT.tangentWS = normalInputs.tangentWS;
                OUT.bitangentWS = normalInputs.bitangentWS;
                return OUT;
            }

            half4 Frag(Varyings IN) : SV_TARGET
            {
                // Normal mapping
                half4 normalSample = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, IN.uv);
                half3 normalTS = UnpackNormalScale(normalSample, _NormalScale);

                half3x3 tangentToWorld = half3x3(IN.tangentWS, IN.bitangentWS, IN.normalWS);
                float3 N = normalize(TransformTangentToWorld(normalTS, tangentToWorld));

                // Lighting, gloss, specular, fresnel
                Light mainLight = GetMainLight();
                float3 V = GetWorldSpaceNormalizeViewDir(IN.positionWS);

                half lambert = saturate(dot(N, mainLight.direction));
                half3 diffuse = _BaseColor.rgb * mainLight.color * lambert;
                half3 ambient = _BaseColor.rgb * SampleSH(N);

                float3 H = normalize(mainLight.direction + V);
                half shininess = exp2(10 * _Smoothness + 1);
                half specular = pow(saturate(dot(N, H)), shininess);

                float2 screenUV = GetNormalizedScreenSpaceUV(IN.positionHCS);
                float3 reflectionDirection = reflect(-V, N);
                half3 reflection = GlossyEnvironmentReflection(reflectionDirection, IN.positionWS, 1 - _Smoothness, 1, screenUV);

                half fresnel = _Reflectivity + (1 - _Reflectivity) * pow(1 - saturate(dot(N, V)), 5);

                half3 color = lerp(diffuse + ambient, reflection, fresnel) + specular * mainLight.color;

                return half4(color, 1);
            }

            ENDHLSL
        }
    }
    Fallback Off
}
