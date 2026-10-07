#ifndef SAGA_STANDARD_SURFACE_INCLUDED
#define SAGA_STANDARD_SURFACE_INCLUDED

#include "Packages/com.saltbox.saga/Shaders/Standard/StandardInput.hlsl"
#include "Packages/com.saltbox.saga/Shaders/Standard/StandardPOM.hlsl"

struct SagaSurface
{
    half3 albedo;
    half3 normalWS;
    half  roughness;
    half  metallic;
    half  ao;
    half3 emissive;
};

#if defined(_WORLD_UV)

struct SagaTriUV
{
    float2 x;
    float2 y;
    float2 z;
};

half3 SagaTriplanarWeights(half3 nGeomWS)
{
    half3 w = pow(abs(nGeomWS), half(_TriplanarSharpness));
    return w * rcp(max(w.x + w.y + w.z, 1e-4h));
}

half4 SagaTriplanar(TEXTURE2D_PARAM(tex, samp), SagaTriUV uv, half3 w)
{
    return SAMPLE_TEXTURE2D(tex, samp, uv.x) * w.x
         + SAMPLE_TEXTURE2D(tex, samp, uv.y) * w.y
         + SAMPLE_TEXTURE2D(tex, samp, uv.z) * w.z;
}

half3 SagaTriplanarNormal(SagaTriUV uv, half3 w, half3 nGeomWS)
{
    half3 tX = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv.x), half(_NormalScale));
    half3 tY = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv.y), half(_NormalScale));
    half3 tZ = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv.z), half(_NormalScale));

    tX = half3(tX.xy + nGeomWS.zy, abs(tX.z) * nGeomWS.x);
    tY = half3(tY.xy + nGeomWS.xz, abs(tY.z) * nGeomWS.y);
    tZ = half3(tZ.xy + nGeomWS.xy, abs(tZ.z) * nGeomWS.z);

    return normalize(tX.zyx * w.x + tY.xzy * w.y + tZ.xyz * w.z);
}

#endif

SagaSurface SagaSampleSurface(float2 uv, float3 positionWS, half3 normalWS, half4 tangentWS,
                              half3 viewDirWS)
{
    SagaSurface s;
    half3 nGeomWS = normalize(normalWS);

    half4 baseMap;
    half3 orm;

#if defined(_WORLD_UV)
    float3 p = positionWS * rcp(max(_MetresPerTile, 1e-4));
    half3  w = SagaTriplanarWeights(nGeomWS);

    SagaTriUV tuv;
    tuv.x = p.zy;
    tuv.y = p.xz;
    tuv.z = p.xy;

#if defined(_POM_ON)
    SagaPOMParams pom;
    pom.depth    = _POMDepth * rcp(max(_MetresPerTile, 1e-4));
    pom.steps    = _POMSteps;
    pom.remapMin = _POMMin;
    pom.remapMax = _POMMax;

    float3 sgn = sign(nGeomWS);
    float2 dxX = ddx(tuv.x), dyX = ddy(tuv.x);
    float2 dxY = ddx(tuv.y), dyY = ddy(tuv.y);
    float2 dxZ = ddx(tuv.z), dyZ = ddy(tuv.z);

    [branch] if (w.x > 0.05h)
        tuv.x = SagaPOMOffset(TEXTURE2D_ARGS(_HeightMap, sampler_HeightMap), pom,
                              tuv.x, float3(viewDirWS.z, viewDirWS.y, viewDirWS.x * sgn.x), dxX, dyX);
    [branch] if (w.y > 0.05h)
        tuv.y = SagaPOMOffset(TEXTURE2D_ARGS(_HeightMap, sampler_HeightMap), pom,
                              tuv.y, float3(viewDirWS.x, viewDirWS.z, viewDirWS.y * sgn.y), dxY, dyY);
    [branch] if (w.z > 0.05h)
        tuv.z = SagaPOMOffset(TEXTURE2D_ARGS(_HeightMap, sampler_HeightMap), pom,
                              tuv.z, float3(viewDirWS.x, viewDirWS.y, viewDirWS.z * sgn.z), dxZ, dyZ);
#endif

    baseMap    = SagaTriplanar(TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap), tuv, w);
    orm        = SagaTriplanar(TEXTURE2D_ARGS(_ORMMap, sampler_ORMMap), tuv, w).rgb;
#if defined(_EMISSION)
    s.emissive = SagaTriplanar(TEXTURE2D_ARGS(_EmissiveMap, sampler_EmissiveMap), tuv, w).rgb;
#else
    s.emissive = 0.0h;
#endif

    s.normalWS = SagaTriplanarNormal(tuv, w, nGeomWS);
#else
    half3 tWS = normalize(tangentWS.xyz);
    half3 bWS = tangentWS.w * cross(nGeomWS, tWS);
    half3x3 tbn = half3x3(tWS, bWS, nGeomWS);

#if defined(_POM_ON)
    SagaPOMParams pom;
    pom.depth    = _POMDepth;
    pom.steps    = _POMSteps;
    pom.remapMin = _POMMin;
    pom.remapMax = _POMMax;

    uv = SagaPOMOffset(TEXTURE2D_ARGS(_HeightMap, sampler_HeightMap), pom,
                       uv, mul(tbn, viewDirWS), ddx(uv), ddy(uv));
#endif

    baseMap    = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
    orm        = SAMPLE_TEXTURE2D(_ORMMap, sampler_ORMMap, uv).rgb;
#if defined(_EMISSION)
    s.emissive = SAMPLE_TEXTURE2D(_EmissiveMap, sampler_EmissiveMap, uv).rgb;
#else
    s.emissive = 0.0h;
#endif

    half3 normalTS = UnpackNormalScale(
        SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv), half(_NormalScale));

    s.normalWS = normalize(TransformTangentToWorld(normalTS, tbn));
#endif

    s.albedo    = baseMap.rgb * _BaseColor.rgb;
    s.ao        = lerp(1.0h, orm.r, half(_OcclusionStrength));
    s.roughness = saturate(orm.g * half(_Roughness));
    s.metallic  = saturate(orm.b * half(_Metallic));
    s.emissive *= _EmissionColor.rgb * half(_EmissiveIntensity);

    return s;
}

#endif
