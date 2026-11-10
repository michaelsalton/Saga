#ifndef SAGA_TERRAIN_FORWARD_PASS_INCLUDED
#define SAGA_TERRAIN_FORWARD_PASS_INCLUDED

#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
#pragma multi_compile_fragment _ _ADDITIONAL_LIGHTS
#pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
#pragma multi_compile_fragment _ _SHADOWS_SOFT
#pragma multi_compile _ _CLUSTER_LIGHT_LOOP
#pragma multi_compile_fog
#pragma multi_compile_instancing

#pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
#pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
#pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"
#include "Packages/com.saltbox.saga/Shaders/Terrain/TerrainInput.hlsl"

struct Attributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    // layer weights from SagaTerrain: r sand, g grass, b rock, a seabed
    half4 color : COLOR;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionHCS : SV_POSITION;
    float3 positionWS : TEXCOORD0;
    half3  normalWS : TEXCOORD1;
    half4  layerWeights : TEXCOORD2;
    half   fogFactor : TEXCOORD3;

    // the terrain is generated at runtime and never lightmapped, so ambient always comes from probes
    half3 vertexSH : TEXCOORD4;
#ifdef USE_APV_PROBE_OCCLUSION
    float4 probeOcclusion : TEXCOORD5;
#endif

    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

Varyings TerrainVert(Attributes IN)
{
    Varyings OUT = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(IN);
    UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

    float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
    float4 positionCS = TransformWorldToHClip(positionWS);

    OUT.positionHCS = positionCS;
    OUT.positionWS = positionWS;
    OUT.normalWS = half3(TransformObjectToWorldNormal(IN.normalOS));
    OUT.layerWeights = IN.color;
    OUT.fogFactor = half(ComputeFogFactor(positionCS.z));

    OUTPUT_SH4(positionWS, OUT.normalWS.xyz, GetWorldSpaceNormalizeViewDir(positionWS), OUT.vertexSH, OUT.probeOcclusion);
    return OUT;
}

half4 TerrainFrag(Varyings IN) : SV_TARGET
{
    UNITY_SETUP_INSTANCE_ID(IN);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

    float3 positionWS = IN.positionWS;
    half3 viewDirWS = GetWorldSpaceNormalizeViewDir(positionWS);
    // interpolation shortens unit normals, which would weaken the whiteout blend and the horizon fade
    half3 geometricNormalWS = normalize(IN.normalWS);
    // SagaTerrain's weights already sum to 1 and interpolation keeps that; this guards any other mesh given this material
    half4 weights = IN.layerWeights / max(dot(IN.layerWeights, 1.0h), 1e-4h);

    float2 sandUV = positionWS.xz * rcp(max(_SandMetresPerTile, 1e-4));
    float2 grassUV = positionWS.xz * rcp(max(_GrassMetresPerTile, 1e-4));
    float3 rockPosition = positionWS * rcp(max(_RockMetresPerTile, 1e-4));
    float2 seabedUV = positionWS.xz * rcp(max(_SeabedMetresPerTile, 1e-4));

    // taken before any branch, while every pixel in the 2x2 quad is still running the same code
    float2 sandDdx = ddx(sandUV);
    float2 sandDdy = ddy(sandUV);
    float2 grassDdx = ddx(grassUV);
    float2 grassDdy = ddy(grassUV);
    float3 rockDdx = ddx(rockPosition);
    float3 rockDdy = ddy(rockPosition);
    float2 seabedDdx = ddx(seabedUV);
    float2 seabedDdy = ddy(seabedUV);

    half3 axisWeights = pow(abs(geometricNormalWS), half(_TriplanarSharpness));
    axisWeights *= rcp(max(axisWeights.x + axisWeights.y + axisWeights.z, 1e-4h));

    // only layers present at this pixel pay for a height sample; most pixels are a single layer
    half4 heights = 0.0h;
    [branch] if (weights.r > 0.0h) heights.r = SAMPLE_TEXTURE2D_GRAD(_SandHeightMap, SAGA_TERRAIN_SAMPLER, sandUV, sandDdx, sandDdy).r;
    [branch] if (weights.g > 0.0h) heights.g = SAMPLE_TEXTURE2D_GRAD(_GrassHeightMap, SAGA_TERRAIN_SAMPLER, grassUV, grassDdx, grassDdy).r;
    [branch] if (weights.b > 0.0h) heights.b = SagaTerrainTriplanarHeight(TEXTURE2D_ARGS(_RockHeightMap, SAGA_TERRAIN_SAMPLER), rockPosition, rockDdx, rockDdy, axisWeights);
    [branch] if (weights.a > 0.0h) heights.a = SAMPLE_TEXTURE2D_GRAD(_SeabedHeightMap, SAGA_TERRAIN_SAMPLER, seabedUV, seabedDdx, seabedDdy).r;

    // height blend: a layer's surface is its height map raised by its weight, and only surfaces near the highest show, so sand fills the gaps between rocks instead of fading over them
    half4 surfaceTops = heights + weights;
    half highest = max(max(surfaceTops.r, surfaceTops.g), max(surfaceTops.b, surfaceTops.a));
    // the step keeps a weightless layer with a tall height map from poking through
    half4 blend = max(surfaceTops - (highest - half(_HeightBlendDepth)), 0.0h) * step(1e-3h, weights);
    blend *= rcp(max(dot(blend, 1.0h), 1e-4h));

    SagaTerrainLayer total = (SagaTerrainLayer)0;
    [branch] if (blend.r > 0.0h)
        SagaTerrainAccumulate(total, SagaTerrainTopDown(TEXTURE2D_ARGS(_SandBaseMap, SAGA_TERRAIN_SAMPLER), TEXTURE2D_ARGS(_SandNormalMap, SAGA_TERRAIN_SAMPLER), TEXTURE2D_ARGS(_SandORMMap, SAGA_TERRAIN_SAMPLER),
                                                     sandUV, sandDdx, sandDdy, _SandTint.rgb, half(_SandNormalScale), geometricNormalWS), blend.r);
    [branch] if (blend.g > 0.0h)
        SagaTerrainAccumulate(total, SagaTerrainTopDown(TEXTURE2D_ARGS(_GrassBaseMap, SAGA_TERRAIN_SAMPLER), TEXTURE2D_ARGS(_GrassNormalMap, SAGA_TERRAIN_SAMPLER), TEXTURE2D_ARGS(_GrassORMMap, SAGA_TERRAIN_SAMPLER),
                                                     grassUV, grassDdx, grassDdy, _GrassTint.rgb, half(_GrassNormalScale), geometricNormalWS), blend.g);
    [branch] if (blend.b > 0.0h)
        SagaTerrainAccumulate(total, SagaTerrainTriplanar(TEXTURE2D_ARGS(_RockBaseMap, SAGA_TERRAIN_SAMPLER), TEXTURE2D_ARGS(_RockNormalMap, SAGA_TERRAIN_SAMPLER), TEXTURE2D_ARGS(_RockORMMap, SAGA_TERRAIN_SAMPLER),
                                                       rockPosition, rockDdx, rockDdy, axisWeights, _RockTint.rgb, half(_RockNormalScale), geometricNormalWS), blend.b);
    [branch] if (blend.a > 0.0h)
        SagaTerrainAccumulate(total, SagaTerrainTopDown(TEXTURE2D_ARGS(_SeabedBaseMap, SAGA_TERRAIN_SAMPLER), TEXTURE2D_ARGS(_SeabedNormalMap, SAGA_TERRAIN_SAMPLER), TEXTURE2D_ARGS(_SeabedORMMap, SAGA_TERRAIN_SAMPLER),
                                                     seabedUV, seabedDdx, seabedDdy, _SeabedTint.rgb, half(_SeabedNormalScale), geometricNormalWS), blend.a);

    half3 normalWS = normalize(total.normalWS);
    half ao = lerp(1.0h, total.orm.r, half(_OcclusionStrength));
    half roughness = saturate(total.orm.g * half(_Roughness));
    // aliased normal-map detail at a distance turns into sparkle; widening roughness by the normal's screen-space variance hides it
    roughness = SagaSpecularAA(roughness, normalWS);

    InputData inputData = (InputData)0;
    inputData.positionWS = positionWS;
    inputData.normalWS = normalWS;
    inputData.viewDirectionWS = viewDirWS;
    inputData.shadowCoord = TransformWorldToShadowCoord(positionWS);
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionHCS);

#if defined(PROBE_VOLUMES_L1) || defined(PROBE_VOLUMES_L2)
    inputData.bakedGI = SAMPLE_GI(IN.vertexSH, GetAbsolutePositionWS(positionWS), normalWS, viewDirWS, IN.positionHCS.xy, IN.probeOcclusion, inputData.shadowMask);
#else
    inputData.bakedGI = SampleSHPixel(IN.vertexSH, normalWS);
#endif

    half3 color = SagaStandardLighting(inputData, geometricNormalWS, total.albedo, 0.0h, roughness, ao);
    return half4(MixFog(color, IN.fogFactor), 1.0h);
}

#endif
