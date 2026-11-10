#ifndef SAGA_TERRAIN_INPUT_INCLUDED
#define SAGA_TERRAIN_INPUT_INCLUDED

// must exist before Standard's input is included, since the macro is expanded inside its UnityPerMaterial
#define SAGA_EXTRA_MATERIAL_PROPERTIES \
    float4 _SandTint; float _SandNormalScale; float _SandMetresPerTile; \
    float4 _GrassTint; float _GrassNormalScale; float _GrassMetresPerTile; \
    float4 _RockTint; float _RockNormalScale; float _RockMetresPerTile; \
    float4 _SeabedTint; float _SeabedNormalScale; float _SeabedMetresPerTile; \
    float _HeightBlendDepth;

#include "Packages/com.saltbox.saga/Shaders/Standard/StandardLighting.hlsl"

// sixteen textures would blow DX11's limit of 16 samplers, so every layer samples through the sand albedo's sampler
TEXTURE2D(_SandBaseMap);
SAMPLER(sampler_SandBaseMap);
TEXTURE2D(_SandNormalMap);
TEXTURE2D(_SandORMMap);
TEXTURE2D(_SandHeightMap);

TEXTURE2D(_GrassBaseMap);
TEXTURE2D(_GrassNormalMap);
TEXTURE2D(_GrassORMMap);
TEXTURE2D(_GrassHeightMap);

TEXTURE2D(_RockBaseMap);
TEXTURE2D(_RockNormalMap);
TEXTURE2D(_RockORMMap);
TEXTURE2D(_RockHeightMap);

TEXTURE2D(_SeabedBaseMap);
TEXTURE2D(_SeabedNormalMap);
TEXTURE2D(_SeabedORMMap);
TEXTURE2D(_SeabedHeightMap);

#define SAGA_TERRAIN_SAMPLER sampler_SandBaseMap

struct SagaTerrainLayer
{
    half3 albedo;
    half3 normalWS;
    half3 orm;
};

// every sample here takes explicit gradients: these run inside per-pixel branches, and a neighbour in the 2x2 quad that skipped the branch would corrupt the implicit mip selection

// planar from above: world x/z are the uvs, so flat ground never stretches and slopes stretch with their steepness
SagaTerrainLayer SagaTerrainTopDown(TEXTURE2D_PARAM(baseMap, baseSampler), TEXTURE2D_PARAM(normalMap, normalSampler), TEXTURE2D_PARAM(ormMap, ormSampler),
                                    float2 uv, float2 uvDdx, float2 uvDdy, half3 tint, half normalScale, half3 geometricNormalWS)
{
    SagaTerrainLayer layer;
    layer.albedo = SAMPLE_TEXTURE2D_GRAD(baseMap, baseSampler, uv, uvDdx, uvDdy).rgb * tint;
    layer.orm = SAMPLE_TEXTURE2D_GRAD(ormMap, ormSampler, uv, uvDdx, uvDdy).rgb;
    half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D_GRAD(normalMap, normalSampler, uv, uvDdx, uvDdy), normalScale);
    // whiteout blend: the texture's x/y tilt the surface along world x/z, and its z scales the geometric normal's up
    layer.normalWS = normalize(half3(normalTS.x + geometricNormalWS.x, abs(normalTS.z) * geometricNormalWS.y, normalTS.y + geometricNormalWS.z));
    return layer;
}

// one planar projection per world axis, weighted by how squarely the surface faces it, so cliffs get a side-on projection instead of vertical streaks
SagaTerrainLayer SagaTerrainTriplanar(TEXTURE2D_PARAM(baseMap, baseSampler), TEXTURE2D_PARAM(normalMap, normalSampler), TEXTURE2D_PARAM(ormMap, ormSampler),
                                      float3 position, float3 positionDdx, float3 positionDdy, half3 axisWeights, half3 tint, half normalScale, half3 geometricNormalWS)
{
    float2 uvX = position.zy;
    float2 uvY = position.xz;
    float2 uvZ = position.xy;

    SagaTerrainLayer layer;
    layer.albedo = (SAMPLE_TEXTURE2D_GRAD(baseMap, baseSampler, uvX, positionDdx.zy, positionDdy.zy).rgb * axisWeights.x
                  + SAMPLE_TEXTURE2D_GRAD(baseMap, baseSampler, uvY, positionDdx.xz, positionDdy.xz).rgb * axisWeights.y
                  + SAMPLE_TEXTURE2D_GRAD(baseMap, baseSampler, uvZ, positionDdx.xy, positionDdy.xy).rgb * axisWeights.z) * tint;
    layer.orm = SAMPLE_TEXTURE2D_GRAD(ormMap, ormSampler, uvX, positionDdx.zy, positionDdy.zy).rgb * axisWeights.x
              + SAMPLE_TEXTURE2D_GRAD(ormMap, ormSampler, uvY, positionDdx.xz, positionDdy.xz).rgb * axisWeights.y
              + SAMPLE_TEXTURE2D_GRAD(ormMap, ormSampler, uvZ, positionDdx.xy, positionDdy.xy).rgb * axisWeights.z;

    half3 normalX = UnpackNormalScale(SAMPLE_TEXTURE2D_GRAD(normalMap, normalSampler, uvX, positionDdx.zy, positionDdy.zy), normalScale);
    half3 normalY = UnpackNormalScale(SAMPLE_TEXTURE2D_GRAD(normalMap, normalSampler, uvY, positionDdx.xz, positionDdy.xz), normalScale);
    half3 normalZ = UnpackNormalScale(SAMPLE_TEXTURE2D_GRAD(normalMap, normalSampler, uvZ, positionDdx.xy, positionDdy.xy), normalScale);
    // whiteout per projection, then each swizzled back from its plane's axes into world axes, as in Standard's triplanar
    normalX = half3(normalX.xy + geometricNormalWS.zy, abs(normalX.z) * geometricNormalWS.x);
    normalY = half3(normalY.xy + geometricNormalWS.xz, abs(normalY.z) * geometricNormalWS.y);
    normalZ = half3(normalZ.xy + geometricNormalWS.xy, abs(normalZ.z) * geometricNormalWS.z);
    layer.normalWS = normalize(normalX.zyx * axisWeights.x + normalY.xzy * axisWeights.y + normalZ.xyz * axisWeights.z);
    return layer;
}

half SagaTerrainTriplanarHeight(TEXTURE2D_PARAM(heightMap, heightSampler), float3 position, float3 positionDdx, float3 positionDdy, half3 axisWeights)
{
    return SAMPLE_TEXTURE2D_GRAD(heightMap, heightSampler, position.zy, positionDdx.zy, positionDdy.zy).r * axisWeights.x
         + SAMPLE_TEXTURE2D_GRAD(heightMap, heightSampler, position.xz, positionDdx.xz, positionDdy.xz).r * axisWeights.y
         + SAMPLE_TEXTURE2D_GRAD(heightMap, heightSampler, position.xy, positionDdx.xy, positionDdy.xy).r * axisWeights.z;
}

void SagaTerrainAccumulate(inout SagaTerrainLayer total, SagaTerrainLayer layer, half weight)
{
    total.albedo += layer.albedo * weight;
    total.normalWS += layer.normalWS * weight;
    total.orm += layer.orm * weight;
}

#endif
