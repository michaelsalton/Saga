Shader "Saga/Water"
{
    Properties
    {
        [Header(Surface)]
        [HDR] _WaterShallowColor ("Shallow Color", Color) = (0.10, 0.40, 0.60, 0.80)
        [HDR] _WaterDeepColor ("Deep Color", Color) = (0.15, 0.6, 1.0, 1.0)
        _WaterOpacity ("Opacity", Range(0, 1)) = 0.8
        _WaterDepthMax ("Deep Color Depth (m)", Range(0.05, 10)) = 1.5

        [Header(Refraction)]
        [HDR] _WaterRefractionColor ("Refraction Tint", Color) = (0.60, 0.85, 0.95, 1.0)
        _RefractionDistortion ("Distortion (internal px)", Range(0, 16)) = 6
        _RefractionFullDepth ("Full Distortion Depth (m)", Range(0.05, 10)) = 1.5
        _RefractionEdgeFade ("Edge Fade (UV)", Range(0, 0.25)) = 0.05

        [Header(Caustics)]
        [HDR] _CausticColor ("Color", Color) = (1.0, 0.98, 0.90, 1.0)
        _CausticStrength ("Strength", Range(0, 1)) = 0.15
        _CausticWaveLength ("Wave Length (m)", Range(0.25, 8)) = 2
        _CausticWarp ("Warp", Range(0, 0.5)) = 0.15
        _CausticSpeed ("Speed (m per s)", Range(0.05, 1)) = 0.15
        _CausticSharpness ("Sharpness", Range(1, 32)) = 10
        _CausticFullDepth ("Fade Out Depth (m)", Range(0.05, 10)) = 1
        _CausticDepthTighten ("Depth Tighten", Range(0, 3)) = 1

        [Header(Reflection)]
        _ReflectionDistortion ("Distortion (internal px)", Range(0, 16)) = 4
        _ReflectionMin ("Min (looking down)", Range(0, 1)) = 0.05
        _ReflectionMax ("Max (grazing)", Range(0, 1)) = 0.6
        _FresnelPower ("Fresnel Power", Range(0.5, 8)) = 2

        [Header(Reflection Distance)]
        _ReflectionFadeStart ("Fade Start (m)", Range(0, 20)) = 1
        _ReflectionFadeDistance ("Fade Range (m)", Range(0.1, 40)) = 6
        _ReflectionFarSky ("Far Sky Wash", Range(0, 1)) = 0.75
        _ReflectionFarOpacity ("Far Opacity", Range(0, 1)) = 1
        _ReflectionFarDistortion ("Far Distortion (x)", Range(0, 4)) = 1.8
        _ReflectionFarBlur ("Far Blur (texels)", Range(0, 4)) = 1

        [Header(Sun Sparkle)]
        [HDR] _SpecColor ("Color", Color) = (1, 1, 1, 1)
        _SpecIntensity ("Intensity", Range(0, 2)) = 1
        _SparkleWaveLength ("Feature Size (m)", Range(0.08, 2)) = 0.3
        _SparkleStrength ("Micro Normal Strength", Range(0, 6)) = 2.5
        _SparkleSpeed ("Drift (m per s)", Range(0, 2)) = 0.3
        _SparkleCutoff ("Cutoff (density)", Range(0, 6)) = 2
        _SparkleFalloff ("Falloff", Range(0.05, 3)) = 1
        _SparklePower ("Power (core tightness)", Range(1, 16)) = 4

        [Header(Foam)]
        [HDR] _FoamColor ("Color", Color) = (1, 1, 1, 1)
        _FoamOpacity ("Opacity", Range(0, 1)) = 1
        _FoamWidth ("Width (m)", Range(0.02, 0.6)) = 0.22
        _FoamPierce ("Pierce Height (m)", Range(0.001, 0.25)) = 0.02
        _FoamSoftness ("Edge Softness (m)", Range(0.001, 0.3)) = 0.06
        _FoamNoiseSize ("Noise Feature Size (m)", Range(0.05, 1)) = 0.3
        _FoamNoiseAmount ("Noise Amount (m)", Range(0, 0.5)) = 0.1
        _FoamNoiseSpeed ("Noise Drift (m per s)", Range(0, 1)) = 0.12

        [Header(Tessellation)]
        _TessellationFactor ("Tessellation Factor", Range(1, 15)) = 4
        _DampeningFactor ("Dampening Factor", Range(0, 1)) = 1

        [Header(Waves)]
        _WaveAmplitude ("Amplitude", Range(0, 5)) = 1.5
        _WaveSpeed ("Speed", Range(0,10)) = 2.5

        [Header(Edge Dampening)]
        _EdgeDampenWidth ("Edge Dampen Width (UV)", Range(0.01, 0.5)) = 0.15
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent"
            "RenderPipeline"="UniversalPipeline"
        }

        Pass
        {
            Name "WaterForward"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma target 5.0
            #pragma vertex Vert
            #pragma hull Hull
            #pragma domain Domain
            #pragma fragment Frag

            #include "Packages/com.saltbox.saga/Shaders/Water/WaterInput.hlsl" // CBUFFER, Textures, Samplers
            #include "Packages/com.saltbox.saga/Shaders/Water/WaterWaves.hlsl" // Wave calculations
            #include "Packages/com.saltbox.saga/Shaders/Water/WaterRefraction.hlsl" // Screen-space refraction
            #include "Packages/com.saltbox.saga/Shaders/Water/WaterDepthColor.hlsl" // Depth based color
            #include "Packages/com.saltbox.saga/Shaders/Water/WaterCaustics.hlsl" // World-projected caustics
            #include "Packages/com.saltbox.saga/Shaders/Water/WaterReflection.hlsl" // PLanar reflection + Fresnel
            #include "Packages/com.saltbox.saga/Shaders/Water/WaterSpecular.hlsl" // Thresholded Blinn-Phong sun glint
            #include "Packages/com.saltbox.saga/Shaders/Water/WaterFoam.hlsl" // Waterline foam from a depth-buffer proximity search
            #include "Packages/com.saltbox.saga/ShaderLibrary/CloudShadows.hlsl" // Global cloud shadow mask

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
            };

            struct ControlPoint
            {
                float4 positionsOS : INTERNALTESSPOS;
                float2 uv : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
            };

            struct TessFactors
            {
                float edge[3] : SV_TessFactor;
                float inside : SV_InsideTessFactor;
            };

            struct Varyings
            {
                float4 positionsHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float3 restPositionWS : TEXCOORD3; // pre-wave. Foam decides everything in this frame
            };

            // Vertex - pass through in OBJECT space
            ControlPoint Vert(Attributes IN)
            {
                ControlPoint o;
                o.positionsOS = IN.positionOS;
                o.uv = IN.uv;
                o.uv1 = IN.uv1;
                return o;
            }

            // Patch Constant - uniform factor on all edges and inside
            TessFactors PatchConstants(InputPatch<ControlPoint, 3> patch,
            uint patchID : SV_PrimitiveID)
            {
                TessFactors f;
                f.edge[0] = _TessellationFactor;
                f.edge[1] = _TessellationFactor;
                f.edge[2] = _TessellationFactor;
                f.inside = _TessellationFactor;
                return f;
            }

            // Hull - per-control-point pass-through
            [domain("tri")]
            [partitioning("fractional_odd")]
            [outputtopology("triangle_cw")]
            [outputcontrolpoints(3)]
            [maxtessfactor(15.0)]
            [patchconstantfunc("PatchConstants")]

            ControlPoint Hull(InputPatch<ControlPoint, 3> patch,
            uint id : SV_OutputControlPointID)
            {
                return patch[id];
            }

            #define BARY_INTERP(field, patch, bary) \
            (patch[0].field * bary.x + patch[1].field * bary.y + patch[2].field * bary.z)

            // Domain - Interpolate object-space pos + uv, then transform object to clip
            [domain("tri")]
            Varyings Domain(TessFactors factors, OutputPatch<ControlPoint, 3> patch,
            float3 bary : SV_DomainLocation)
            {
                Varyings o;
                float4 positionsOS = BARY_INTERP(positionsOS, patch, bary);
                o.uv = BARY_INTERP(uv, patch, bary);
                float2 uv1 = BARY_INTERP(uv1, patch, bary);

                float edgeDampen = SagaWaveEdgeDampen(uv1);
                SagaWaveResult waves = SagaCalculateWaves(positionsOS.xyz, edgeDampen);

                VertexPositionInputs positions = GetVertexPositionInputs(waves.position);

                o.normalWS = TransformObjectToWorldNormal(waves.normal);
                o.positionWS = positions.positionWS;
                o.restPositionWS = TransformObjectToWorld(positionsOS.xyz);
                //o.positionsHCS = TransformObjectToHClip(waves.position);
                o.positionsHCS = positions.positionCS;
                return o;
            }

            // Fragment
            half4 Frag(Varyings IN) : SV_TARGET
            {
                float3 finalNormal = normalize(IN.normalWS);
                float2 screenUV = GetNormalizedScreenSpaceUV(IN.positionsHCS);

                SagaRefractionResult refraction =
                    SagaCalculateRefraction(screenUV, IN.positionsHCS.z, IN.positionWS.y, finalNormal);

                half cloudSurf  = SagaCloudShadow(IN.positionWS);
                half cloudFloor = SagaCloudShadow(refraction.floorWS);
                half3 waterColor = SagaWaterDepthColor(refraction.columnDepthAtUV) * cloudSurf;
                half3 color = lerp(refraction.color, waterColor, _WaterOpacity);

                color += SagaCaustics(refraction.floorWS, refraction.columnDepthAtUV) * cloudFloor;

                float reflectionWeight;
                half3 reflection = SagaWaterReflection(screenUV, finalNormal,
                                                       refraction.surfaceEye, reflectionWeight);
                color = lerp(color, reflection,
                             SagaWaterFresnel(finalNormal, IN.positionWS) * reflectionWeight);

                color += SagaWaterSpecular(finalNormal, IN.positionWS) * cloudSurf;

                half foam = SagaWaterFoam(screenUV, IN.positionWS, IN.restPositionWS);
                color = lerp(color, _FoamColor.rgb * cloudSurf, foam * _FoamOpacity);

                return half4(color, 1.0);
            }

            ENDHLSL
        }
    }
    Fallback Off
}
