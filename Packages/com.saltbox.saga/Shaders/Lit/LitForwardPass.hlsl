#ifndef SAGA_LIT_FORWARD_PASS_INCLUDED
#define SAGA_LIT_FORWARD_PASS_INCLUDED

#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
#pragma multi_compile_fragment _ _ADDITIONAL_LIGHTS
#pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
#pragma multi_compile_fragment _ _SHADOWS_SOFT
#pragma multi_compile _ _CLUSTER_LIGHT_LOOP
#pragma multi_compile_fog
#pragma multi_compile_instancing

#pragma multi_compile _ LIGHTMAP_ON
#pragma multi_compile _ DIRLIGHTMAP_COMBINED
#pragma multi_compile _ USE_LEGACY_LIGHTMAPS
#pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
#pragma multi_compile _ SHADOWS_SHADOWMASK
#pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
#pragma multi_compile_fragment _ LIGHTMAP_BICUBIC_SAMPLING
#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"

#pragma shader_feature_local_fragment _WORLD_UV
#pragma shader_feature_local_fragment _RELIEF

#include "Packages/com.saltbox.saga/Shaders/Lit/LitLighting.hlsl"
#include "Packages/com.saltbox.saga/Shaders/Lit/LitSurface.hlsl"

// Only shaders that opt in (Saga/Lit) pull this in. Saga/Ground compiles this same file without the
// define and is untouched -- see the HLSLINCLUDE block in Lit.shader for why that matters.
#ifdef SAGA_OCCLUDABLE
#include "Packages/com.saltbox.saga/ShaderLibrary/WorldOcclusion.hlsl"
#endif

#ifndef SAGA_MODIFY_SURFACE
#define SAGA_MODIFY_SURFACE(surf, positionWS)
#endif

struct Attributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float4 tangentOS : TANGENT;
    float2 uv : TEXCOORD0;
    float2 staticLightmapUV : TEXCOORD1;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionHCS : SV_POSITION;
    float3 positionWS : TEXCOORD0;
    half3  normalWS : TEXCOORD1;
    half4  tangentWS : TEXCOORD2;
    float2 uv : TEXCOORD3;
    half   fogFactor : TEXCOORD4;

    DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 5);
#ifdef USE_APV_PROBE_OCCLUSION
    float4 probeOcclusion : TEXCOORD6;
#endif

    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

Varyings Vert(Attributes IN)
{
    Varyings OUT = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(IN);
    UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

    VertexPositionInputs p = GetVertexPositionInputs(IN.positionOS.xyz);
    VertexNormalInputs n = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);

    OUT.positionHCS = p.positionCS;
    OUT.positionWS = p.positionWS;
    OUT.normalWS = half3(n.normalWS);
    OUT.tangentWS = half4(n.tangentWS, IN.tangentOS.w * GetOddNegativeScale());
    OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
    OUT.fogFactor = half(ComputeFogFactor(p.positionCS.z));

    OUTPUT_LIGHTMAP_UV(IN.staticLightmapUV, unity_LightmapST, OUT.staticLightmapUV);
    OUTPUT_SH4(p.positionWS, OUT.normalWS.xyz,
               GetWorldSpaceNormalizeViewDir(p.positionWS),
               OUT.vertexSH, OUT.probeOcclusion);

    return OUT;
}

half4 Frag(Varyings IN) : SV_TARGET
{
    UNITY_SETUP_INSTANCE_ID(IN);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

    half3 viewDirWS = GetWorldSpaceNormalizeViewDir(IN.positionWS);

    SagaSurface surf = SagaSampleSurface(IN.uv, IN.positionWS, IN.normalWS, IN.tangentWS, viewDirWS);

    SAGA_MODIFY_SURFACE(surf, IN.positionWS);

    InputData inputData  = (InputData)0;
    inputData.positionWS  = IN.positionWS;
    inputData.normalWS = surf.normalWS;
    inputData.viewDirectionWS = viewDirWS;
    inputData.shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionHCS);

#if !defined(LIGHTMAP_ON) && (defined(PROBE_VOLUMES_L1) || defined(PROBE_VOLUMES_L2))
    inputData.bakedGI = SAMPLE_GI(IN.vertexSH,
                                  GetAbsolutePositionWS(inputData.positionWS),
                                  inputData.normalWS,
                                  inputData.viewDirectionWS,
                                  IN.positionHCS.xy,
                                  IN.probeOcclusion,
                                  inputData.shadowMask);
#else
    inputData.bakedGI = SAMPLE_GI(IN.staticLightmapUV, IN.vertexSH, inputData.normalWS);
    inputData.shadowMask = SAMPLE_SHADOWMASK(IN.staticLightmapUV);
#endif

    half3 col = SagaLitLighting(inputData, surf.albedo, surf.metallic, surf.roughness, surf.ao);

    col += surf.emissive;

    col = MixFog(col, IN.fogFactor);

#ifdef SAGA_OCCLUDABLE
    SagaOcclusionClip(SagaOcclusionMask(IN.positionWS), IN.positionHCS.xy);
#endif

    return half4(col, 1.0h);
}

#endif
