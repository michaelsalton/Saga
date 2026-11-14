#ifndef SAGA_STANDARD_POM_INCLUDED
#define SAGA_STANDARD_POM_INCLUDED

#include "Packages/com.saltbox.saga/Shaders/Standard/StandardInput.hlsl"

// Parallax Occlusion Mapping Algorithm

struct SagaPOMParams
{
    float depth;
    float minSteps;
    float maxSteps;
    float remapMin;
    float remapMax;
};

float SagaPOMHeight(TEXTURE2D_PARAM(heightMap, samplerHeightMap), SagaPOMParams pom,
                    float2 uv, float2 dx, float2 dy)
{
    float raw = SAMPLE_TEXTURE2D_GRAD(heightMap, samplerHeightMap, uv, dx, dy).r;
    return saturate((raw - pom.remapMin) * rcp(max(pom.remapMax - pom.remapMin, 1e-4)));
}

float2 SagaPOMOffset(TEXTURE2D_PARAM(heightMap, samplerHeightMap), SagaPOMParams pom,
                     float2 uv, float3 vTS, float2 dx, float2 dy, out float hitHeight)
{
    hitHeight = 1.0;
    // view leaves this plane instead of entering it; marching would offset the wrong way
    [branch] if (vTS.z <= 0.0)
        return uv;

    float2 shear  = vTS.xy * rcp(max(vTS.z, 0.15)) * pom.depth;

    // grazing rays cover more uv, so they get more steps to keep the same texel spacing
    int    steps  = max((int)round(lerp(pom.maxSteps, pom.minSteps, vTS.z)), 1);
    float  stepH  = rcp((float)steps);
    float2 stepUV = shear * stepH;

    float  rayH  = 1.0;
    float2 curUV = uv;
    float  curH  = SagaPOMHeight(TEXTURE2D_ARGS(heightMap, samplerHeightMap), pom, curUV, dx, dy);

    float  prevRayH = rayH;
    float2 prevUV   = curUV;
    float  prevH    = curH;

    [loop]
    for (int i = 0; i < steps && curH < rayH; i++)
    {
        prevRayH = rayH;  prevUV = curUV;  prevH = curH;

        rayH  -= stepH;
        curUV -= stepUV;
        curH   = SagaPOMHeight(TEXTURE2D_ARGS(heightMap, samplerHeightMap), pom, curUV, dx, dy);
    }

    float a = prevRayH - prevH;
    float b = curH - rayH;
    float hitFraction = saturate(a * rcp(max(a + b, 1e-5)));
    // the shadow march starts here; starting at the top surface instead would leave nothing above it to occlude
    hitHeight = lerp(prevRayH, rayH, hitFraction);
    return lerp(prevUV, curUV, hitFraction);
}

float2 SagaPOMOffset(TEXTURE2D_PARAM(heightMap, samplerHeightMap), SagaPOMParams pom,
                     float2 uv, float3 vTS, float2 dx, float2 dy)
{
    float hitHeight;
    return SagaPOMOffset(TEXTURE2D_ARGS(heightMap, samplerHeightMap), pom, uv, vTS, dx, dy, hitHeight);
}

// 1 = lit, 0 = the height field fully blocks the light from this point
half SagaPOMSelfShadow(TEXTURE2D_PARAM(heightMap, samplerHeightMap), SagaPOMParams pom,
                       float2 hitUV, float hitHeight, float3 lightDirTS, float2 dx, float2 dy, float hardness)
{
    // light below the surface plane: NdotL already darkens it, and the shear would blow up
    [branch] if (lightDirTS.z <= 0.0 || hitHeight >= 1.0)
        return 1.0h;

    // climbing toward the light moves +L.xy, the reverse of the view march's descent along -V.xy
    float2 shear = lightDirTS.xy * rcp(max(lightDirTS.z, 0.15)) * pom.depth;
    int steps = max((int)round(lerp(pom.maxSteps, pom.minSteps, lightDirTS.z)), 1);
    // only the slab above the hit can occlude, so the steps cover that and not the full depth
    float stepHeight = (1.0 - hitHeight) * rcp((float)steps);
    float2 stepUV = shear * stepHeight;

    float rayHeight = hitHeight;
    float2 sampleUV = hitUV;
    float occlusion = 0.0;

    [loop]
    for (int i = 1; i <= steps; i++)
    {
        rayHeight += stepHeight;
        sampleUV += stepUV;
        float sampleHeight = SagaPOMHeight(TEXTURE2D_ARGS(heightMap, samplerHeightMap), pom, sampleUV, dx, dy);
        // near occluders give hard edges and far ones soft, faking the penumbra widening with distance
        occlusion = max(occlusion, (sampleHeight - rayHeight) * (1.0 - (float)i / steps));
        // already fully dark; further samples can't make it lit again
        if (occlusion * hardness >= 1.0) break;
    }
    return half(1.0 - saturate(occlusion * hardness));
}

SagaPOMParams SagaStandardPOMParams()
{
    SagaPOMParams pom;
    pom.depth = _POMDepth;
    pom.minSteps = _POMMinSteps;
    pom.maxSteps = _POMSteps;
    pom.remapMin = _POMMin;
    pom.remapMax = _POMMax;
    return pom;
}

// rows are tangent, bitangent, normal, so mul(frame, worldVector) lands in tangent space
half3x3 SagaStandardPOMFrame(half3 normalWS, half4 tangentWS, half faceSign)
{
    half3 nGeomWS = normalize(normalWS);
    half3 tWS = normalize(tangentWS.xyz);
    half3 bWS = tangentWS.w * cross(nGeomWS, tWS);
    // on a back face the visible side is opposite the normal, so marches descend and climb the other way
    return half3x3(tWS, bWS, nGeomWS * faceSign);
}

// forward and depth passes both march through here from the same raw varyings, so they agree on which pixels survive the clip
float2 SagaStandardPOMUV(float2 uv, half3 normalWS, half4 tangentWS, half3 viewDirWS, half faceSign,
                         float2 uvDdx, float2 uvDdy, out float hitHeight)
{
    float3 viewDirTS = mul(SagaStandardPOMFrame(normalWS, tangentWS, faceSign), viewDirWS);
    return SagaPOMOffset(TEXTURE2D_ARGS(_HeightMap, sampler_HeightMap), SagaStandardPOMParams(),
                         uv, viewDirTS, uvDdx, uvDdy, hitHeight);
}

float2 SagaStandardPOMUV(float2 uv, half3 normalWS, half4 tangentWS, half3 viewDirWS, half faceSign,
                         float2 uvDdx, float2 uvDdy)
{
    float hitHeight;
    return SagaStandardPOMUV(uv, normalWS, tangentWS, viewDirWS, faceSign, uvDdx, uvDdy, hitHeight);
}

half SagaStandardPOMSelfShadow(float2 hitUV, float hitHeight, half3 normalWS, half4 tangentWS, half3 lightDirWS,
                               half faceSign, float2 uvDdx, float2 uvDdy)
{
    float3 lightDirTS = mul(SagaStandardPOMFrame(normalWS, tangentWS, faceSign), lightDirWS);
    return SagaPOMSelfShadow(TEXTURE2D_ARGS(_HeightMap, sampler_HeightMap), SagaStandardPOMParams(),
                             hitUV, hitHeight, lightDirTS, uvDdx, uvDdy, _POMShadowHardness);
}

// TRANSFORM_TEX maps the mesh's 0-1 uv onto this rectangle, so a hit outside it lies past the mesh edge and the pixel shouldn't exist
void SagaPOMSilhouetteClip(float2 parallaxUV)
{
    float2 cornerA = _BaseMap_ST.zw;
    float2 cornerB = _BaseMap_ST.zw + _BaseMap_ST.xy;
    // min/max because negative tiling flips which corner is lower
    float2 inside = step(min(cornerA, cornerB), parallaxUV) * step(parallaxUV, max(cornerA, cornerB));
    clip(inside.x * inside.y - 0.5);
}

#endif
