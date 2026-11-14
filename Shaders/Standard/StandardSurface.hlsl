#ifndef SAGA_STANDARD_SURFACE_INCLUDED
#define SAGA_STANDARD_SURFACE_INCLUDED

#include "Packages/com.saltbox.saga/Shaders/Standard/StandardInput.hlsl"
#include "Packages/com.saltbox.saga/Shaders/Standard/StandardPOM.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"

struct SagaSurface
{
    half3 albedo;
    half alpha;
    half3 normalWS;
    half  roughness;
    half  metallic;
    half  ao;
    half3 emissive;
    // POM self-shadow toward the main light; 1 = lit
    half  parallaxShadow;
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

// gradients come from the pre-parallax uv; the offset uv jumps at height edges and would drop those pixels to the smallest mip
half4 SagaTriplanar(TEXTURE2D_PARAM(tex, samp), SagaTriUV uv, SagaTriUV uvDdx, SagaTriUV uvDdy, half3 w)
{
    return SAMPLE_TEXTURE2D_GRAD(tex, samp, uv.x, uvDdx.x, uvDdy.x) * w.x
         + SAMPLE_TEXTURE2D_GRAD(tex, samp, uv.y, uvDdx.y, uvDdy.y) * w.y
         + SAMPLE_TEXTURE2D_GRAD(tex, samp, uv.z, uvDdx.z, uvDdy.z) * w.z;
}

half3 SagaTriplanarNormal(SagaTriUV uv, SagaTriUV uvDdx, SagaTriUV uvDdy, half3 w, half3 nGeomWS)
{
    half3 tX = UnpackNormalScale(SAMPLE_TEXTURE2D_GRAD(_NormalMap, sampler_NormalMap, uv.x, uvDdx.x, uvDdy.x), half(_NormalScale));
    half3 tY = UnpackNormalScale(SAMPLE_TEXTURE2D_GRAD(_NormalMap, sampler_NormalMap, uv.y, uvDdx.y, uvDdy.y), half(_NormalScale));
    half3 tZ = UnpackNormalScale(SAMPLE_TEXTURE2D_GRAD(_NormalMap, sampler_NormalMap, uv.z, uvDdx.z, uvDdy.z), half(_NormalScale));

    tX = half3(tX.xy + nGeomWS.zy, abs(tX.z) * nGeomWS.x);
    tY = half3(tY.xy + nGeomWS.xz, abs(tY.z) * nGeomWS.y);
    tZ = half3(tZ.xy + nGeomWS.xy, abs(tZ.z) * nGeomWS.z);

    return normalize(tX.zyx * w.x + tY.xzy * w.y + tZ.xyz * w.z);
}

#endif

SagaSurface SagaSampleSurface(float2 uv, float3 positionWS, half3 normalWS, half4 tangentWS,
                              half3 viewDirWS, half faceSign)
{
    SagaSurface s;
    s.parallaxShadow = 1.0h;
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

    SagaTriUV tuvDdx;
    tuvDdx.x = ddx(tuv.x);
    tuvDdx.y = ddx(tuv.y);
    tuvDdx.z = ddx(tuv.z);
    SagaTriUV tuvDdy;
    tuvDdy.x = ddy(tuv.x);
    tuvDdy.y = ddy(tuv.y);
    tuvDdy.z = ddy(tuv.z);

#if defined(_POM_ON)
    SagaPOMParams pom;
    pom.depth = _POMDepth * rcp(max(_MetresPerTile, 1e-4));
    pom.minSteps = _POMMinSteps;
    pom.maxSteps = _POMSteps;
    pom.remapMin = _POMMin;
    pom.remapMax = _POMMax;

    // on a back face the visible side is opposite the normal, so height grows toward the other side of each plane
    float3 sgn = sign(nGeomWS) * faceSign;

    [branch] if (w.x > 0.05h)
        tuv.x = SagaPOMOffset(TEXTURE2D_ARGS(_HeightMap, sampler_HeightMap), pom,
                              tuv.x, float3(viewDirWS.z, viewDirWS.y, viewDirWS.x * sgn.x), tuvDdx.x, tuvDdy.x);
    [branch] if (w.y > 0.05h)
        tuv.y = SagaPOMOffset(TEXTURE2D_ARGS(_HeightMap, sampler_HeightMap), pom,
                              tuv.y, float3(viewDirWS.x, viewDirWS.z, viewDirWS.y * sgn.y), tuvDdx.y, tuvDdy.y);
    [branch] if (w.z > 0.05h)
        tuv.z = SagaPOMOffset(TEXTURE2D_ARGS(_HeightMap, sampler_HeightMap), pom,
                              tuv.z, float3(viewDirWS.x, viewDirWS.y, viewDirWS.z * sgn.z), tuvDdx.z, tuvDdy.z);
#endif

    baseMap = SagaTriplanar(TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap), tuv, tuvDdx, tuvDdy, w);
    orm = SagaTriplanar(TEXTURE2D_ARGS(_ORMMap, sampler_ORMMap), tuv, tuvDdx, tuvDdy, w).rgb;
#if defined(_EMISSION)
    s.emissive = SagaTriplanar(TEXTURE2D_ARGS(_EmissiveMap, sampler_EmissiveMap), tuv, tuvDdx, tuvDdy, w).rgb;
#else
    s.emissive = 0.0h;
#endif

    s.normalWS = SagaTriplanarNormal(tuv, tuvDdx, tuvDdy, w, nGeomWS);
#else
    half3 tWS = normalize(tangentWS.xyz);
    half3 bWS = tangentWS.w * cross(nGeomWS, tWS);
    half3x3 tbn = half3x3(tWS, bWS, nGeomWS);

    // gradients come from the pre-parallax uv; the offset uv jumps at height edges and would drop those pixels to the smallest mip
    float2 uvDdx = ddx(uv);
    float2 uvDdy = ddy(uv);

#if defined(_POM_ON)
    // raw normalWS, not nGeomWS: the depth passes march from the raw varying, and a rounding difference would speckle the clipped edge
    float hitHeight;
    uv = SagaStandardPOMUV(uv, normalWS, tangentWS, viewDirWS, faceSign, uvDdx, uvDdy, hitHeight);
#if defined(_POM_SILHOUETTE)
    SagaPOMSilhouetteClip(uv);
#endif
#if defined(_POM_SELF_SHADOW)
    // _MainLightPosition, not GetMainLight(): Terrain compiles this file before URP's Lighting.hlsl is included
    s.parallaxShadow = SagaStandardPOMSelfShadow(uv, hitHeight, normalWS, tangentWS, _MainLightPosition.xyz, faceSign, uvDdx, uvDdy);
#endif
#endif

    baseMap = SAMPLE_TEXTURE2D_GRAD(_BaseMap, sampler_BaseMap, uv, uvDdx, uvDdy);
    orm = SAMPLE_TEXTURE2D_GRAD(_ORMMap, sampler_ORMMap, uv, uvDdx, uvDdy).rgb;
#if defined(_EMISSION)
    s.emissive = SAMPLE_TEXTURE2D_GRAD(_EmissiveMap, sampler_EmissiveMap, uv, uvDdx, uvDdy).rgb;
#else
    s.emissive = 0.0h;
#endif

    half3 normalTS = UnpackNormalScale(
        SAMPLE_TEXTURE2D_GRAD(_NormalMap, sampler_NormalMap, uv, uvDdx, uvDdy), half(_NormalScale));

#if defined(_DETAIL)
    half detailMask = SAMPLE_TEXTURE2D_GRAD(_DetailMask, sampler_DetailMask, uv, uvDdx, uvDdy).a;
    float2 detailUV = uv * _DetailAlbedoMap_ST.xy + _DetailAlbedoMap_ST.zw;
    float2 detailUVDdx = uvDdx * _DetailAlbedoMap_ST.xy;
    float2 detailUVDdy = uvDdy * _DetailAlbedoMap_ST.xy;

    half3 detailAlbedo = SAMPLE_TEXTURE2D_GRAD(_DetailAlbedoMap, sampler_DetailAlbedoMap, detailUV, detailUVDdx, detailUVDdy).rgb;
    detailAlbedo = lerp(1.0h, 2.0h * detailAlbedo, half(_DetailAlbedoMapScale));
    baseMap.rgb *= lerp(1.0h, detailAlbedo, detailMask);

    half3 detailNormalTS = normalize(UnpackNormalScale(
        SAMPLE_TEXTURE2D_GRAD(_DetailNormalMap, sampler_DetailNormalMap, detailUV, detailUVDdx, detailUVDdy), half(_DetailNormalMapScale)));
    normalTS = lerp(normalTS, BlendNormalRNM(normalTS, detailNormalTS), detailMask);
#endif

    s.normalWS = normalize(TransformTangentToWorld(normalTS, tbn));
#endif

    s.albedo = baseMap.rgb * _BaseColor.rgb;
    s.alpha = baseMap.a * _BaseColor.a;
    // strength above 1 extrapolates past the map's value, so clamp before it goes negative and subtracts light
    s.ao = saturate(lerp(1.0h, orm.r, half(_OcclusionStrength)));
    s.roughness = saturate(orm.g * half(_Roughness));
    s.metallic = saturate(orm.b * half(_Metallic));
    s.emissive *= _EmissionColor.rgb * half(_EmissiveIntensity);

    return s;
}

#endif
