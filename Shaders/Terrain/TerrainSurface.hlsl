#ifndef SAGA_TERRAIN_SURFACE_INCLUDED
#define SAGA_TERRAIN_SURFACE_INCLUDED

#include "Packages/com.saltbox.saga/Shaders/Terrain/TerrainInput.hlsl"
// for SagaSurface, which the forward pass's SAGA_MODIFY_SURFACE hook expects back
#include "Packages/com.saltbox.saga/Shaders/Standard/StandardSurface.hlsl"

struct SagaTerrainLayer
{
    half3 albedo;
    half3 normalTS;
    half roughness;
    half metallic;
    half ao;
};

SagaTerrainLayer SagaSampleTerrainLayer(
    TEXTURE2D_PARAM(albedoMap, albedoSampler),
    TEXTURE2D_PARAM(normalMap, normalSampler),
    TEXTURE2D_PARAM(maskMap, maskSampler),
    float2 layerUV, half3 albedoTint, half normalScale, half metallic, half smoothness,
    half hasMask, half4 maskRemapScale, half4 maskRemapOffset)
{
    SagaTerrainLayer layer;
    layer.albedo = SAMPLE_TEXTURE2D(albedoMap, albedoSampler, layerUV).rgb * albedoTint;
    layer.normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(normalMap, normalSampler, layerUV), normalScale);

    // terrain layer mask slot carries ARM: R occlusion, G roughness, B metallic
    half4 mask = SAMPLE_TEXTURE2D(maskMap, maskSampler, layerUV) * maskRemapScale + maskRemapOffset;
    layer.ao = lerp(1.0h, mask.r, hasMask);
    layer.roughness = lerp(1.0h - smoothness, mask.g, hasMask);
    layer.metallic = lerp(metallic, mask.b, hasMask);
    return layer;
}

SagaSurface SagaSampleTerrainSurface(float2 terrainUV, half3 geometricNormalWS)
{
    // splatmap texel centres sit on heightmap vertices, so shift half a texel or painting drifts off the brush
    float2 controlUV = (terrainUV * (_Control_TexelSize.zw - 1.0) + 0.5) * _Control_TexelSize.xy;
    half4 weights = SAMPLE_TEXTURE2D(_Control, sampler_Control, controlUV);

    SagaTerrainLayer layer0 = SagaSampleTerrainLayer(
        TEXTURE2D_ARGS(_Splat0, sampler_Splat0), TEXTURE2D_ARGS(_Normal0, sampler_Normal0), TEXTURE2D_ARGS(_Mask0, sampler_Mask0),
        terrainUV * _Splat0_ST.xy + _Splat0_ST.zw, _DiffuseRemapScale0.rgb, _NormalScale0, _Metallic0, _Smoothness0,
        _LayerHasMask0, _MaskMapRemapScale0, _MaskMapRemapOffset0);
    SagaTerrainLayer layer1 = SagaSampleTerrainLayer(
        TEXTURE2D_ARGS(_Splat1, sampler_Splat0), TEXTURE2D_ARGS(_Normal1, sampler_Normal0), TEXTURE2D_ARGS(_Mask1, sampler_Mask0),
        terrainUV * _Splat1_ST.xy + _Splat1_ST.zw, _DiffuseRemapScale1.rgb, _NormalScale1, _Metallic1, _Smoothness1,
        _LayerHasMask1, _MaskMapRemapScale1, _MaskMapRemapOffset1);
    SagaTerrainLayer layer2 = SagaSampleTerrainLayer(
        TEXTURE2D_ARGS(_Splat2, sampler_Splat0), TEXTURE2D_ARGS(_Normal2, sampler_Normal0), TEXTURE2D_ARGS(_Mask2, sampler_Mask0),
        terrainUV * _Splat2_ST.xy + _Splat2_ST.zw, _DiffuseRemapScale2.rgb, _NormalScale2, _Metallic2, _Smoothness2,
        _LayerHasMask2, _MaskMapRemapScale2, _MaskMapRemapOffset2);
    SagaTerrainLayer layer3 = SagaSampleTerrainLayer(
        TEXTURE2D_ARGS(_Splat3, sampler_Splat0), TEXTURE2D_ARGS(_Normal3, sampler_Normal0), TEXTURE2D_ARGS(_Mask3, sampler_Mask0),
        terrainUV * _Splat3_ST.xy + _Splat3_ST.zw, _DiffuseRemapScale3.rgb, _NormalScale3, _Metallic3, _Smoothness3,
        _LayerHasMask3, _MaskMapRemapScale3, _MaskMapRemapOffset3);

    SagaSurface surface;
    surface.albedo = layer0.albedo * weights.r + layer1.albedo * weights.g + layer2.albedo * weights.b + layer3.albedo * weights.a;
    surface.roughness = dot(weights, half4(layer0.roughness, layer1.roughness, layer2.roughness, layer3.roughness));
    surface.metallic = dot(weights, half4(layer0.metallic, layer1.metallic, layer2.metallic, layer3.metallic));
    surface.ao = dot(weights, half4(layer0.ao, layer1.ao, layer2.ao, layer3.ao));
    surface.alpha = 1.0h;
    // Standard's Frag adds emissive unconditionally
    surface.emissive = 0.0h;
    surface.parallaxShadow = 1.0h;

    // weighted average of tangent-space normals, renormalised; flattens slightly where layers overlap
    half3 normalTS = normalize(layer0.normalTS * weights.r + layer1.normalTS * weights.g + layer2.normalTS * weights.b + layer3.normalTS * weights.a);

    // terrain meshes have no tangents; uv runs along world X and Z and terrain can't rotate, so build the frame from those
    half3 tangentWS = normalize(cross(geometricNormalWS, half3(0.0h, 0.0h, 1.0h)));
    half3 bitangentWS = cross(tangentWS, geometricNormalWS);
    surface.normalWS = normalize(TransformTangentToWorld(normalTS, half3x3(tangentWS, bitangentWS, geometricNormalWS)));

    return surface;
}

#endif
