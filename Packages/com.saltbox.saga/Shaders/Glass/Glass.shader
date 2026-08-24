Shader "Saga/Glass"
{
    Properties
    {
        [Header(Surface)]
        [Normal] _NormalMap ("Normal Map", 2D) = "bump" {}
        _NormalScale ("Normal Scale", Range(0, 2)) = 1
        _WavinessSlope ("Waviness Slope (0 = flat)", Range(0, 0.5)) = 0.12
        _WavinessScale ("Waviness Waves Per UV", Range(0.5, 24)) = 3

        [Header(Transmission)]
        _TransmissionTint ("Tint Through The Glass", Color) = (0.82, 0.94, 0.90, 1)
        _AbsorptionDensity ("Absorption Density", Range(0, 32)) = 12
        _ThicknessMetres ("Thickness (m)", Range(0.002, 0.5)) = 0.08

        [Header(Refraction)]
        _RefractionDistortion ("Distortion At Rim (internal px)", Range(0, 12)) = 2
        _RefractionEdgeFade ("Screen Edge Fade (UV)", Range(0, 0.25)) = 0.05

        [Header(Reflection)]
        [HDR] _ReflectColor ("Reflection Color", Color) = (0.55, 0.68, 0.85, 1)
        _ReflectMin ("Min (head on, 0.04 = physical)", Range(0, 1)) = 0.06
        _ReflectMax ("Max (grazing)", Range(0, 1)) = 0.5
        _FresnelPower ("Fresnel Power (5 = Schlick)", Range(0.5, 8)) = 2
        [IntRange] _ReflectBands ("Fresnel Bands (0 = smooth)", Range(0, 8)) = 3
        _ReflectBandSoftness ("Band Softness", Range(0, 1)) = 0

        [Header(Sun Glint)]
        [HDR] _GlintColor ("Color", Color) = (1, 1, 1, 1)
        _GlintIntensity ("Intensity", Range(0, 4)) = 1
        _GlintCutoff ("Cutoff (N dot L)", Range(0, 1)) = 0.85
        _GlintFalloff ("Falloff", Range(0.01, 0.5)) = 0.12
        _GlintPower ("Power (core tightness)", Range(1, 16)) = 4

        [Header(Composite)]
        _CompositeBlend ("Composite Blend (1 = replace)", Range(0, 1)) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull (Off for single quads)", Float) = 2
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent"
            "RenderPipeline"="UniversalPipeline"
        }
        LOD 100

        Pass
        {
            Name "GlassForward"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull [_Cull]

            HLSLPROGRAM

            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.saltbox.saga/Shaders/Glass/GlassInput.hlsl"        // CBUFFER, textures, opaque + depth texture declarations
            #include "Packages/com.saltbox.saga/Shaders/Glass/GlassSurface.hlsl"      // shading normal, incl. the procedural waviness
            #include "Packages/com.saltbox.saga/Shaders/Glass/GlassAbsorption.hlsl"   // Beer-Lambert tint over the slab chord
            #include "Packages/com.saltbox.saga/Shaders/Glass/GlassRefraction.hlsl"   // screen-space refraction with a foreground clamp
            #include "Packages/com.saltbox.saga/Shaders/Glass/GlassFresnel.hlsl"      // Schlick reflectance, banded
            #include "Packages/com.saltbox.saga/Shaders/Glass/GlassSpecular.hlsl"     // sun-anchored hard glint
            #include "Packages/com.saltbox.saga/ShaderLibrary/CloudShadows.hlsl"
            #include "Packages/com.saltbox.saga/ShaderLibrary/WorldOcclusion.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3  normalWS : TEXCOORD1;
                half4  tangentWS : TEXCOORD2;
                float2 uv : TEXCOORD3;
            };

            Varyings Vert(Attributes IN)
            {
                Varyings OUT = (Varyings)0;

                VertexPositionInputs p = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs n = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);

                OUT.positionHCS = p.positionCS;
                OUT.positionWS = p.positionWS;
                OUT.normalWS = half3(n.normalWS);
                OUT.tangentWS = half4(n.tangentWS, IN.tangentOS.w * GetOddNegativeScale());
                OUT.uv = IN.uv;

                return OUT;
            }

            half4 Frag(Varyings IN, FRONT_FACE_TYPE facing : FRONT_FACE_SEMANTIC) : SV_TARGET
            {
                half faceSign = IS_FRONT_VFACE(facing, 1.0h, -1.0h);

                float3 N = SagaGlassNormal(IN.uv, IN.normalWS * faceSign, IN.tangentWS);
                float3 V = GetWorldSpaceNormalizeViewDir(IN.positionWS);
                float2 screenUV = GetNormalizedScreenSpaceUV(IN.positionHCS);
                half3 transmittance = SagaGlassTransmittance(N, V);

                SagaGlassRefraction refraction =
                    SagaGlassRefract(screenUV, IN.positionHCS.z, N, transmittance);

                half3 color = refraction.color;
                half3 reflected = half3(_ReflectColor.rgb) * SagaCloudShadow(IN.positionWS);
                color = lerp(color, reflected, SagaGlassFresnel(N, IN.positionWS));

                color += SagaGlassGlint(N, IN.positionWS);
                SagaOcclusionClip(SagaOcclusionMask(IN.positionWS), IN.positionHCS.xy);

                return half4(color, half(_CompositeBlend));
            }

            ENDHLSL
        }
    }
    Fallback Off
}
